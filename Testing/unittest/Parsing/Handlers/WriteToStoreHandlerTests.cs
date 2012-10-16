/*

Copyright dotNetRDF Project 2009-12
dotnetrdf-develop@lists.sf.net

------------------------------------------------------------------------

This file is part of dotNetRDF.

dotNetRDF is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

dotNetRDF is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.

You should have received a copy of the GNU General Public License
along with dotNetRDF.  If not, see <http://www.gnu.org/licenses/>.

------------------------------------------------------------------------

dotNetRDF may alternatively be used under the LGPL or MIT License

http://www.gnu.org/licenses/lgpl.html
http://www.opensource.org/licenses/mit-license.php

If these licenses are not suitable for your intended use please contact
us at the above stated email address to discuss alternative
terms.

*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VDS.RDF.Parsing;
using VDS.RDF.Parsing.Handlers;
using VDS.RDF.Storage;
using VDS.RDF.Test.Storage;

namespace VDS.RDF.Test.Parsing.Handlers
{
    /// <summary>
    /// Summary description for WriteToStoreHandlerTests
    /// </summary>
    [TestClass]
    public class WriteToStoreHandlerTests
    {
        private readonly Uri TestGraphUri = new Uri("http://example.org/WriteToStoreHandlerTest");
        private readonly Uri TestBNodeUri = new Uri("http://example.org/WriteToStoreHandlerTest/BNodes");

        private void EnsureTestData()
        {
            if (!System.IO.File.Exists("temp.ttl"))
            {
                Graph g = new Graph();
                EmbeddedResourceLoader.Load(g, "VDS.RDF.Configuration.configuration.ttl");
                g.SaveToFile("temp.ttl");
            }
        }

        private void TestWriteToStoreHandler(IStorageProvider manager)
        {
            //First ensure that our test file exists
            EnsureTestData();

            //Try to ensure that the target Graph does not exist
            if (manager.DeleteSupported)
            {
                manager.DeleteGraph(TestGraphUri);
            }
            else
            {
                Graph g = new Graph();
                g.BaseUri = TestGraphUri;
                manager.SaveGraph(g);
            }

            Graph temp = new Graph();
            try
            {
                manager.LoadGraph(temp, TestGraphUri);
                Assert.IsTrue(temp.IsEmpty, "Unable to ensure that Target Graph in Store is empty prior to running Test");
            }
            catch
            {
                //An Error Loading the Graph is OK
            }

            WriteToStoreHandler handler = new WriteToStoreHandler(manager, TestGraphUri, 100);
            TurtleParser parser = new TurtleParser();
            parser.Load(handler, "temp.ttl");

            manager.LoadGraph(temp, TestGraphUri);
            Assert.IsFalse(temp.IsEmpty, "Graph should not be empty");

            Graph orig = new Graph();
            orig.LoadFromFile("temp.ttl");

            Assert.AreEqual(orig, temp, "Graphs should be equal");
        }

        private void TestWriteToStoreDatasetsHandler(IStorageProvider manager)
        {
            NodeFactory factory = new NodeFactory();
            INode a = factory.CreateUriNode(new Uri("http://example.org/a"));
            INode b = factory.CreateUriNode(new Uri("http://example.org/b"));
            INode c = factory.CreateUriNode(new Uri("http://example.org/c"));
            INode d = factory.CreateUriNode(new Uri("http://example.org/d"));

            Uri graphB = new Uri("http://example.org/graphs/b");
            Uri graphD = new Uri("http://example.org/graphs/d");

            //Try to ensure that the target Graphs do not exist
            if (manager.DeleteSupported)
            {
                manager.DeleteGraph(TestGraphUri);
                manager.DeleteGraph(graphB);
                manager.DeleteGraph(graphD);
            }
            else
            {
                Graph g = new Graph();
                g.BaseUri = TestGraphUri;
                manager.SaveGraph(g);
                g.BaseUri = graphB;
                manager.SaveGraph(g);
                g.BaseUri = graphD;
                manager.SaveGraph(g);
            }

            //Do the parsing and thus the loading
            WriteToStoreHandler handler = new WriteToStoreHandler(manager, TestGraphUri);
            NQuadsParser parser = new NQuadsParser();
            parser.Load(handler, new StreamReader("writetostore.nq"));

            //Load the expected Graphs
            Graph def = new Graph();
            manager.LoadGraph(def, TestGraphUri);
            Graph gB = new Graph();
            manager.LoadGraph(gB, graphB);
            Graph gD = new Graph();
            manager.LoadGraph(gD, graphD);

            Assert.AreEqual(2, def.Triples.Count, "Should be two triples in the default Graph");
            Assert.IsTrue(def.ContainsTriple(new Triple(a, a, a)), "Default Graph should have the a triple");
            Assert.AreEqual(1, gB.Triples.Count, "Should be one triple in the b Graph");
            Assert.IsTrue(gB.ContainsTriple(new Triple(b, b, b)), "b Graph should have the b triple");
            Assert.IsTrue(def.ContainsTriple(new Triple(c, c, c)), "Default Graph should have the c triple");
            Assert.AreEqual(1, gD.Triples.Count, "Should be one triple in the d Graph");
            Assert.IsTrue(gD.ContainsTriple(new Triple(d, d, d)), "d Graph should have the d triple");
        }

        private void TestWriteToStoreHandlerWithBNodes(IStorageProvider manager)
        {
            String fragment = "@prefix : <http://example.org/>. :subj :has [ a :BNode ; :with \"value\" ] .";

            //Try to ensure that the target Graph does not exist
            if (manager.DeleteSupported)
            {
                manager.DeleteGraph(TestBNodeUri);
            } 
            else 
            {
                Graph temp = new Graph();
                temp.BaseUri = TestBNodeUri;
                manager.SaveGraph(temp);
            }

            //Then write to the store
            TurtleParser parser = new TurtleParser();
            WriteToStoreHandler handler = new WriteToStoreHandler(manager, TestBNodeUri, 1);
            parser.Load(handler, new StringReader(fragment));

            //Then load back the data and check it
            Graph g = new Graph();
            manager.LoadGraph(g, TestBNodeUri);

            Assert.AreEqual(3, g.Triples.Count, "Should be 3 Triples");
            List<IBlankNode> nodes = g.Nodes.BlankNodes().ToList();
            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = 0; j < nodes.Count; j++)
                {
                    if (i == j) continue;
                    Assert.AreEqual(nodes[i], nodes[j], "All Blank Nodes should be the same");
                }
            }
        }

        [TestMethod, ExpectedException(typeof(ArgumentNullException))]
        public void ParsingWriteToStoreHandlerBadInstantiation()
        {
            WriteToStoreHandler handler = new WriteToStoreHandler(null, null);
        }

        [TestMethod, ExpectedException(typeof(ArgumentException))]
        public void ParsingWriteToStoreHandlerBadInstantiation2()
        {
            WriteToStoreHandler handler = new WriteToStoreHandler(new ReadOnlyConnector(new InMemoryManager()), null);
        }

        [TestMethod, ExpectedException(typeof(ArgumentException))]
        public void ParsingWriteToStoreHandlerBadInstantiation4()
        {
            WriteToStoreHandler handler = new WriteToStoreHandler(new InMemoryManager(), null, 0);
        }

        [TestMethod]
        public void ParsingWriteToStoreHandlerVirtuoso()
        {
            VirtuosoManager virtuoso = VirtuosoTest.GetConnection();
            this.TestWriteToStoreHandler(virtuoso);
        }

        [TestMethod]
        public void ParsingWriteToStoreHandlerAllegroGraph()
        {
            AllegroGraphConnector agraph = AllegroGraphTests.GetConnection();
            this.TestWriteToStoreHandler(agraph);
        }

        [TestMethod]
        public void ParsingWriteToStoreHandlerFuseki()
        {
            try
            {
                Options.UriLoaderCaching = false;
                FusekiConnector fuseki = FusekiTest.GetConnection();
                this.TestWriteToStoreHandler(fuseki);
            }
            finally
            {
                Options.UriLoaderCaching = true;
            }
        }

        [TestMethod]
        public void ParsingWriteToStoreHandlerInMemory()
        {
            InMemoryManager mem = new InMemoryManager();
            this.TestWriteToStoreHandler(mem);
        }

        [TestMethod]
        public void ParsingWriteToStoreHandlerDatasetsInMemory()
        {
            InMemoryManager manager = new InMemoryManager();
            this.TestWriteToStoreDatasetsHandler(manager);
        }

        [TestMethod]
        public void ParsingWriteToStoreHandlerDatasetsVirtuoso()
        {
            VirtuosoManager virtuoso = VirtuosoTest.GetConnection();
            this.TestWriteToStoreDatasetsHandler(virtuoso);
        }

        [TestMethod]
        public void ParsingWriteToStoreHandlerBNodesAcrossBatchesAllegroGraph()
        {
            AllegroGraphConnector agraph = AllegroGraphTests.GetConnection();
            this.TestWriteToStoreHandlerWithBNodes(agraph);
        }

        [TestMethod]
        public void ParsingWriteToStoreHandlerBNodesAcrossBatchesFuseki()
        {
            FusekiConnector fuseki = FusekiTest.GetConnection();
            this.TestWriteToStoreHandlerWithBNodes(fuseki);
        }

        [TestMethod]
        public void ParsingWriteToStoreHandlerBNodesAcrossBatchesInMemory()
        {
            InMemoryManager manager = new InMemoryManager();
            this.TestWriteToStoreHandlerWithBNodes(manager);
        }

        [TestMethod]
        public void ParsingWriteToStoreHandlerBNodesAcrossBatchesVirtuoso()
        {
            VirtuosoManager virtuoso = VirtuosoTest.GetConnection();
            this.TestWriteToStoreHandlerWithBNodes(virtuoso);
        }
    }
}
