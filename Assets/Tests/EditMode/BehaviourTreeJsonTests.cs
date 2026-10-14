using System.IO;
using Framework.BehaviourTree;
using NUnit.Framework;
using UnityEngine;

namespace Framework.Tests
{
    /// <summary>行为树 JSON：换成 Newtonsoft 之后，旧的 JsonUtility 产物仍能原样读回。</summary>
    public sealed class BehaviourTreeJsonTests
    {
        [Test]
        public void ExistingBtJsonRoundTripsWithoutLosingFields()
        {
            var path = Path.Combine(Application.dataPath, "Bundles", "BehaviourTrees", "MeleeChaser.bt.json");
            var original = BtTreeSerializer.FromJson(File.ReadAllText(path));
            Assert.Greater(original.Nodes.Count, 0);
            Assert.AreEqual("root", original.RootNodeId);

            var again = BtTreeSerializer.FromJson(BtTreeSerializer.ToJson(original));
            Assert.AreEqual(original.TreeName, again.TreeName);
            Assert.AreEqual(original.RootNodeId, again.RootNodeId);
            Assert.AreEqual(original.Nodes.Count, again.Nodes.Count);
            for (var i = 0; i < original.Nodes.Count; i++)
            {
                var a = original.Nodes[i];
                var b = again.Nodes[i];
                Assert.AreEqual(a.Id, b.Id);
                Assert.AreEqual(a.Kind, b.Kind);
                Assert.AreEqual(a.TypeId, b.TypeId);
                Assert.AreEqual(a.IntParam, b.IntParam);
                Assert.AreEqual(a.FloatParam, b.FloatParam);
                Assert.AreEqual(a.FpRawParam, b.FpRawParam);
                CollectionAssert.AreEqual(a.ChildIds, b.ChildIds);
                Assert.AreEqual(a.Params.Count, b.Params.Count);
            }
        }

        [Test]
        public void MissingFieldsKeepTheirInitializers()
        {
            var definition = BtTreeSerializer.FromJson(
                "{\"Version\":2,\"RootNodeId\":\"a\",\"Nodes\":[{\"Id\":\"a\",\"Kind\":1,\"EditorPosition\":{\"x\":12.5,\"y\":-3}}]}");
            var node = definition.Nodes[0];
            Assert.AreEqual(string.Empty, node.TypeId);
            Assert.IsNotNull(node.ChildIds);
            Assert.IsTrue(node.FailFast);
            Assert.AreEqual(12.5f, node.EditorPosition.x);
            Assert.AreEqual(-3f, node.EditorPosition.y);
        }

        [Test]
        public void VersionOneTreesAreMigrated()
        {
            var definition = BtTreeSerializer.FromJson(
                "{\"Version\":1,\"RootNodeId\":\"w\",\"Nodes\":[{\"Id\":\"w\",\"Kind\":" + (int)BtNodeKind.WaitTime +
                ",\"FloatParam\":1.5,\"FailFast\":false}]}");
            Assert.AreEqual(BtTreeDefinition.CurrentVersion, definition.Version);
            Assert.IsTrue(definition.Nodes[0].FailFast);
            Assert.IsTrue(definition.Nodes[0].HasFpRaw);
        }
    }
}
