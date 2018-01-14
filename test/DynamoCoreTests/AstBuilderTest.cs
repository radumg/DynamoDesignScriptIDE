﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Dynamo.Engine.CodeGeneration;
using Dynamo.Graph;
using Dynamo.Graph.Nodes;
using Dynamo.Models;
using NUnit.Framework;

namespace Dynamo.Tests
{
    [Category("DSExecution")]
    class AstBuilderTest : DynamoModelTestBase
    {
        private const int shuffleCount = 10;

        private class ShuffleUtil<T>
        {
            private readonly Random random;
            private List<T> list;

            public List<T> ShuffledList
            {
                get
                {
                    list = list.OrderBy(i => random.Next()).ToList();
                    return list;
                }
            }

            public ShuffleUtil(List<T> list)
            {
                random = new Random();
                this.list = list;
            }
        }

        [Test]
        public void TestCompileToAstNodes1()
        {
            string openPath = Path.Combine(TestDirectory, @"core\astbuilder\complex.dyn");
            OpenModel(openPath);

            var builder = new AstBuilder(null);
            var astNodes = builder.CompileToAstNodes(CurrentDynamoModel.CurrentWorkspace.Nodes, CompilationContext.None, false);
            var codeGen = new ProtoCore.CodeGenDS(astNodes.SelectMany(t => t.Item2));
            string code = codeGen.GenerateCode();
            Console.WriteLine(code);
        }

        [Test]
        public void TestSortNode1()
        {
            // The connections of CBNs are
            // 
            //  1 <----> 2
            //
            string openPath = Path.Combine(TestDirectory, @"core\astbuilder\cyclic.dyn");
            OpenModel(openPath);

            var sortedNodes = AstBuilder.TopologicalSort(CurrentDynamoModel.CurrentWorkspace.Nodes);
            Assert.AreEqual(sortedNodes.Count(), 2);
        }

        [Test]
        public void TestSortNode2()
        {
            // The connections of CBNs are
            // 
            //      +----> 2
            //     /
            //   1 ----> 3
            //     \    
            //      +----> 4
            // 
            string openPath = Path.Combine(TestDirectory, @"core\astbuilder\multioutputs.dyn");
            OpenModel(openPath);

            var nodes = CurrentDynamoModel.CurrentWorkspace.Nodes.ToList();

            var shuffle = new ShuffleUtil<NodeModel>(nodes);

            for (int i = 0; i < shuffleCount; ++i)
            {
                var sortedNodes = AstBuilder.TopologicalSort(shuffle.ShuffledList).ToList();
                Assert.AreEqual(sortedNodes.Count(), 4);

                List<int> nickNames = sortedNodes.Select(node => Int32.Parse(node.NickName)).ToList();

                var nodePosMap = new Dictionary<int, int>();
                for (int idx = 0; idx < nickNames.Count; ++idx)
                {
                    nodePosMap[nickNames[idx]] = idx;
                }

                // no matter input nodes in whatever order, there invariants 
                // should hold
                Assert.IsTrue(nodePosMap[2] > nodePosMap[1]);
                Assert.IsTrue(nodePosMap[3] > nodePosMap[1]);
                Assert.IsTrue(nodePosMap[4] > nodePosMap[1]);
            }
        }

        [Test]
        public void TestSortNode3()
        {
            // The connections of CBNs are
            // 
            //   1 ----+ 
            //          \
            //   2 ----> 4
            //          /
            //   3 ----+
            // 
            string openPath = Path.Combine(TestDirectory, @"core\astbuilder\multiinputs.dyn");
            OpenModel(openPath);

            var nodes = CurrentDynamoModel.CurrentWorkspace.Nodes.ToList();

            var shuffle = new ShuffleUtil<NodeModel>(nodes);

            for (int i = 0; i < shuffleCount; ++i)
            {
                var sortedNodes = AstBuilder.TopologicalSort(shuffle.ShuffledList).ToList();
                Assert.AreEqual(sortedNodes.Count(), 4);

                List<int> nickNames = sortedNodes.Select(node => Int32.Parse(node.NickName)).ToList();

                var nodePosMap = new Dictionary<int, int>();
                for (int idx = 0; idx < nickNames.Count; ++idx)
                {
                    nodePosMap[nickNames[idx]] = idx;
                }

                // no matter input nodes in whatever order, there invariants 
                // should hold
                Assert.IsTrue(nodePosMap[1] < nodePosMap[4]);
                Assert.IsTrue(nodePosMap[2] < nodePosMap[4]);
                Assert.IsTrue(nodePosMap[3] < nodePosMap[4]);
            }
        }

        [Test]
        public void TestSortNode4()
        {
            // The connections of CBNs are
            //   
            //  +---------------+    
            //  |               |
            //  |               v
            //  2 ----> 3 ----> 1
            // 
            string openPath = Path.Combine(TestDirectory, @"core\astbuilder\tri.dyn");
            OpenModel(openPath);

            var nodes = CurrentDynamoModel.CurrentWorkspace.Nodes.ToList();

            var shuffle = new ShuffleUtil<NodeModel>(nodes);

            for (int i = 0; i < shuffleCount; ++i)
            {
                var sortedNodes = AstBuilder.TopologicalSort(shuffle.ShuffledList).ToList();
                Assert.AreEqual(sortedNodes.Count(), 3);

                List<int> nickNames = sortedNodes.Select(node => Int32.Parse(node.NickName)).ToList();

                var nodePosMap = new Dictionary<int, int>();
                for (int idx = 0; idx < nickNames.Count; ++idx)
                {
                    nodePosMap[nickNames[idx]] = idx;
                }

                // no matter input nodes in whatever order, there invariants 
                // should hold
                Assert.IsTrue(nodePosMap[2] < nodePosMap[3]);
                Assert.IsTrue(nodePosMap[3] < nodePosMap[1]);
            }
        }


        [Test]
        public void TestSortNode5()
        {
            // The connections of CBNs are
            //   
            // 1 <---- 2 <----> 3 <---- 4
            //
            string openPath = Path.Combine(TestDirectory, @"core\astbuilder\linear.dyn");
            OpenModel(openPath);

            var nodes = CurrentDynamoModel.CurrentWorkspace.Nodes.ToList();

            var shuffle = new ShuffleUtil<NodeModel>(nodes);

            for (int i = 0; i < shuffleCount; ++i)
            {
                var sortedNodes = AstBuilder.TopologicalSort(shuffle.ShuffledList).ToList();
                Assert.AreEqual(sortedNodes.Count(), 4);

                List<int> nickNames = sortedNodes.Select(node => Int32.Parse(node.NickName)).ToList();

                var nodePosMap = new Dictionary<int, int>();
                for (int idx = 0; idx < nickNames.Count; ++idx)
                {
                    nodePosMap[nickNames[idx]] = idx;
                }

                // no matter input nodes in whatever order, there invariants 
                // should hold
                Assert.IsTrue(nodePosMap[4] < nodePosMap[3]);
                Assert.IsTrue(nodePosMap[4] < nodePosMap[2]);
                Assert.IsTrue(nodePosMap[3] < nodePosMap[1]);
                Assert.IsTrue(nodePosMap[2] < nodePosMap[1]);
            }
        }

        [Test]
        public void TestSortNode6()
        {
            // The connections of CBNs are
            //
            //                   1
            //                   ^
            //                   |
            //                   2
            //                   ^
            //                   |
            //  6 <---- 4 <----> 3 <----> 5 ----> 7          8 <----> 9
            // 
            string openPath = Path.Combine(TestDirectory, @"core\astbuilder\complex.dyn");
            OpenModel(openPath);

            var nodes = CurrentDynamoModel.CurrentWorkspace.Nodes.ToList();

            var shuffle = new ShuffleUtil<NodeModel>(nodes);

            for (int i = 0; i < shuffleCount; ++i)
            {
                nodes = shuffle.ShuffledList;

                var sortedNodes = AstBuilder.TopologicalSort(nodes).ToList();
                Assert.AreEqual(sortedNodes.Count(), 9);

                var nickNames = sortedNodes.Select(node => Int32.Parse(node.NickName)).ToList();
                var nodePosMap = new Dictionary<int, int>();
                for (int idx = 0; idx < nickNames.Count; ++idx)
                {
                    nodePosMap[nickNames[idx]] = idx;
                }

                // no matter input nodes in whatever order, there invariants 
                // should hold
                Assert.IsTrue(nodePosMap[1] > nodePosMap[2]);
                Assert.IsTrue(nodePosMap[6] > nodePosMap[4]);
                Assert.IsTrue(nodePosMap[7] > nodePosMap[5]);
            }
        }

        [Test]
        public void TestTopologicalSortForGraph1()
        {
            // The connections of CBNs are
            // 
            //  1 <----> 2
            //
            string openPath = Path.Combine(TestDirectory, @"core\astbuilder\cyclic.dyn");
            OpenModel(openPath);

            var sortedNodes = AstBuilder.TopologicalSortForGraph(CurrentDynamoModel.CurrentWorkspace.Nodes);
            Assert.AreEqual(sortedNodes.Count(), 2);
        }

        [Test]
        public void TestTopologicalSortForGraph2()
        {
            // The connections of CBNs are
            // 
            //      +----> 2
            //     /
            //   1 ----> 3
            //     \    
            //      +----> 4
            // 
            string openPath = Path.Combine(TestDirectory, @"core\astbuilder\multioutputs.dyn");
            OpenModel(openPath);

            var nodes = CurrentDynamoModel.CurrentWorkspace.Nodes.ToList();

            var shuffle = new ShuffleUtil<NodeModel>(nodes);

            for (int i = 0; i < shuffleCount; ++i)
            {
                var sortedNodes = AstBuilder.TopologicalSortForGraph(shuffle.ShuffledList).ToList();
                Assert.AreEqual(sortedNodes.Count(), 4);

                List<int> nickNames = sortedNodes.Select(node => Int32.Parse(node.NickName)).ToList();

                var nodePosMap = new Dictionary<int, int>();
                for (int idx = 0; idx < nickNames.Count; ++idx)
                {
                    nodePosMap[nickNames[idx]] = idx;
                }

                // no matter input nodes in whatever order, there invariants 
                // should hold
                Assert.IsTrue(nodePosMap[2] > nodePosMap[1]);
                Assert.IsTrue(nodePosMap[3] > nodePosMap[1]);
                Assert.IsTrue(nodePosMap[4] > nodePosMap[1]);
            }
        }

        [Test]
        public void TestTopologicalSortForGraph3()
        {
            // The connections of CBNs are
            // 
            //   1 ----+ 
            //          \
            //   2 ----> 4
            //          /
            //   3 ----+
            // 
            string openPath = Path.Combine(TestDirectory, @"core\astbuilder\multiinputs.dyn");
            OpenModel(openPath);

            var nodes = CurrentDynamoModel.CurrentWorkspace.Nodes.ToList();

            var shuffle = new ShuffleUtil<NodeModel>(nodes);

            for (int i = 0; i < shuffleCount; ++i)
            {
                var sortedNodes = AstBuilder.TopologicalSortForGraph(shuffle.ShuffledList).ToList();
                Assert.AreEqual(sortedNodes.Count(), 4);

                List<int> nickNames = sortedNodes.Select(node => Int32.Parse(node.NickName)).ToList();

                var nodePosMap = new Dictionary<int, int>();
                for (int idx = 0; idx < nickNames.Count; ++idx)
                {
                    nodePosMap[nickNames[idx]] = idx;
                }

                // no matter input nodes in whatever order, there invariants 
                // should hold
                Assert.IsTrue(nodePosMap[1] < nodePosMap[4]);
                Assert.IsTrue(nodePosMap[2] < nodePosMap[4]);
                Assert.IsTrue(nodePosMap[3] < nodePosMap[4]);
                Assert.IsTrue(nodePosMap[1] < nodePosMap[2]);
                Assert.IsTrue(nodePosMap[2] < nodePosMap[3]);
                Assert.IsTrue(nodePosMap[3] < nodePosMap[4]);
            }
        }

        [Test]
        public void TestTopologicalSortForGraph4()
        {
            // The connections of CBNs are
            //   
            //  +---------------+    
            //  |               |
            //  |               v
            //  2 ----> 3 ----> 1
            // 
            string openPath = Path.Combine(TestDirectory, @"core\astbuilder\tri.dyn");
            OpenModel(openPath);

            var nodes = CurrentDynamoModel.CurrentWorkspace.Nodes.ToList();

            var shuffle = new ShuffleUtil<NodeModel>(nodes);

            for (int i = 0; i < shuffleCount; ++i)
            {
                var sortedNodes = AstBuilder.TopologicalSortForGraph(shuffle.ShuffledList).ToList();
                Assert.AreEqual(sortedNodes.Count(), 3);

                List<int> nickNames = sortedNodes.Select(node => Int32.Parse(node.NickName)).ToList();

                var nodePosMap = new Dictionary<int, int>();
                for (int idx = 0; idx < nickNames.Count; ++idx)
                {
                    nodePosMap[nickNames[idx]] = idx;
                }

                // no matter input nodes in whatever order, there invariants 
                // should hold
                Assert.IsTrue(nodePosMap[2] < nodePosMap[3]);
                Assert.IsTrue(nodePosMap[3] < nodePosMap[1]);
            }
        }


        [Test]
        public void TestTopologicalSortForGraph5()
        {
            // The connections of CBNs are
            //   
            // 1 <---- 2 <----> 3 <---- 4
            //
            string openPath = Path.Combine(TestDirectory, @"core\astbuilder\linear.dyn");
            OpenModel(openPath);

            var nodes = CurrentDynamoModel.CurrentWorkspace.Nodes.ToList();

            var shuffle = new ShuffleUtil<NodeModel>(nodes);

            for (int i = 0; i < shuffleCount; ++i)
            {
                var sortedNodes = AstBuilder.TopologicalSortForGraph(shuffle.ShuffledList).ToList();
                Assert.AreEqual(sortedNodes.Count(), 4);

                List<int> nickNames = sortedNodes.Select(node => Int32.Parse(node.NickName)).ToList();

                var nodePosMap = new Dictionary<int, int>();
                for (int idx = 0; idx < nickNames.Count; ++idx)
                {
                    nodePosMap[nickNames[idx]] = idx;
                }

                // no matter input nodes in whatever order, there invariants 
                // should hold
                Assert.IsTrue(nodePosMap[4] < nodePosMap[3]);
                Assert.IsTrue(nodePosMap[4] < nodePosMap[2]);
                Assert.IsTrue(nodePosMap[3] < nodePosMap[1]);
                Assert.IsTrue(nodePosMap[2] < nodePosMap[1]);
            }
        }

        [Test]
        public void TestTopologicalSortForGraph6()
        {
            // The connections of CBNs are
            //
            //                   1
            //                   ^
            //                   |
            //                   2
            //                   ^
            //                   |
            //  6 <---- 4 <----> 3 <----> 5 ----> 7          8 ----> 9
            // 
            string openPath = Path.Combine(TestDirectory, @"core\astbuilder\complex.dyn");
            OpenModel(openPath);

            var nodes = CurrentDynamoModel.CurrentWorkspace.Nodes.ToList();

            var shuffle = new ShuffleUtil<NodeModel>(nodes);

            for (int i = 0; i < shuffleCount; ++i)
            {
                nodes = shuffle.ShuffledList;

                var sortedNodes = AstBuilder.TopologicalSortForGraph(nodes).ToList();
                Assert.AreEqual(sortedNodes.Count(), 9);

                var nickNames = sortedNodes.Select(node => Int32.Parse(node.NickName)).ToList();
                var nodePosMap = new Dictionary<int, int>();
                for (int idx = 0; idx < nickNames.Count; ++idx)
                {
                    nodePosMap[nickNames[idx]] = idx;
                }

                // no matter input nodes in whatever order, there invariants 
                // should hold
                Assert.IsTrue(nodePosMap[1] > nodePosMap[2]);
                Assert.IsTrue(nodePosMap[6] > nodePosMap[4]);
                Assert.IsTrue(nodePosMap[7] > nodePosMap[5]);
                Assert.IsTrue(nodePosMap[5] > nodePosMap[3]
                    || nodePosMap[2] > nodePosMap[3]
                    || nodePosMap[4] > nodePosMap[3]);
                Assert.IsTrue(nodePosMap[9] > nodePosMap[8]);
            }
        }

        [Test]
        public void TestTopologicalSortForGraph7()
        {
            // The connections of CBNs are
            //
            //     1 ----> 2 ----> 3 ----+
            //                           |
            //     4 ----> 5 ----> 6 ----+
            //                           |
            //                           +----> 13
            //                           |
            //     7 ----> 8 ----> 9 ----+
            //                           |
            //    10 ---> 11 ---->12 ----+
            string openPath = Path.Combine(TestDirectory, @"core\astbuilder\multiinputs2.dyn");
            OpenModel(openPath);

            var nodes = CurrentDynamoModel.CurrentWorkspace.Nodes.ToList();

            var shuffle = new ShuffleUtil<NodeModel>(nodes);

            for (int i = 0; i < shuffleCount; ++i)
            {
                nodes = shuffle.ShuffledList;

                var sortedNodes = AstBuilder.TopologicalSortForGraph(nodes).ToList();
                Assert.AreEqual(sortedNodes.Count(), 13);

                var nickNames = sortedNodes.Select(node => Int32.Parse(node.NickName)).ToList();
                Assert.IsTrue(nickNames.SequenceEqual(Enumerable.Range(1, 13)));
            }
        }
    }
}
