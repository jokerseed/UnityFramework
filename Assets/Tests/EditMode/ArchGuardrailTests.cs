using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Framework.BehaviourTree;
using Framework.Core;
using Framework.Editor.Determinism;
using Framework.Events;
using Framework.FixedMath;
using Framework.GAS;
using Framework.GAS.Combat;
using NUnit.Framework;

namespace Framework.Tests
{
    /// <summary>阶段 6：扫描器范围、普通伤害结算和固定帧等待。不改模拟公式。</summary>
    public sealed class ArchGuardrailTests
    {
        [Test]
        public void ScannerRejectsFloatInBehaviourTreeRuntime()
        {
            var violations = new List<string>();
            SimDeterminismScanner.CollectViolations(
                "Assets/Framework/BehaviourTree/BtSample.cs",
                "float speed = 1f;",
                violations);
            Assert.Greater(violations.Count, 0);
        }

        [Test]
        public void ScannerIgnoresFloatInBehaviourTreeConfig()
        {
            var violations = new List<string>();
            SimDeterminismScanner.CollectViolations(
                "Assets/Framework/BehaviourTree/Config/BtConfigNode.cs",
                "public float FloatValue;",
                violations);
            Assert.IsEmpty(violations);
        }

        [Test]
        public void ScannerAllowsTickFloatBridge()
        {
            var violations = new List<string>();
            SimDeterminismScanner.CollectViolations(
                "Assets/Framework/Core/Tick/ITickable.cs",
                "void Tick(float deltaTime);",
                violations);
            Assert.IsEmpty(violations);
        }

        [Test]
        public void IntegerDivisionStaysExactForNonZeroDivisor()
        {
            Assert.AreEqual((FP)2, (FP)10 / (FP)5);
            Assert.AreEqual((FP)1, (FP)5 % (FP)2);
        }

        [Test]
        public void ApplyDamageSubtractsHealthWhenDefenseIsZero()
        {
            var bus = new ZeroGcEventBus();
            var target = new AbilitySystemComponent(new ActorId(1));
            target.InitializeHealth((FP)100);
            var context = new DamageContext(new ActorId(2), target.ActorId, (FP)30, "hit");

            Assert.IsTrue(target.ApplyDamage(in context, bus));
            Assert.AreEqual((FP)70, target.Attributes.GetCurrentValue(BattleConstants.Health));
        }

        [Test]
        public void WaitFramesSucceedsOnTheRequestedTick()
        {
            var tree = new Framework.BehaviourTree.BehaviourTree(new BtWaitFrames(2));
            var context = new BtContext(new BtBlackboard());
            Assert.AreEqual(BtStatus.Running, tree.Tick(context));
            Assert.AreEqual(BtStatus.Success, tree.Tick(context));
        }

        [Test]
        public void FrameworkAssembliesAreAcyclicAndGamePlayDoesNotReferenceLuban()
        {
            var assets = FindAssetsFolder();
            var graph = new Dictionary<string, List<string>>();
            foreach (var file in Directory.GetFiles(assets, "*.asmdef", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(file);
                var name = Regex.Match(text, "\"name\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
                if (!name.StartsWith("Framework.", System.StringComparison.Ordinal))
                {
                    continue;
                }

                var refs = new List<string>();
                var block = Regex.Match(text, "\"references\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
                foreach (Match match in Regex.Matches(block.Groups[1].Value, "\"([^\"]+)\""))
                {
                    refs.Add(match.Groups[1].Value);
                }

                graph[name] = refs;
            }

            Assert.IsFalse(graph["Framework.Core"].Contains("Framework.Events"));
            Assert.IsFalse(graph["Framework.GamePlay"].Contains("Generated.Luban"));
            Assert.IsTrue(graph["Framework.ECS.Physics"].Contains("Framework.LockstepPhysics"));
            AssertNoCycle(graph);
            foreach (var pair in graph)
            {
                for (var i = 0; i < pair.Value.Count; i++)
                {
                    var reference = pair.Value[i];
                    if (reference.StartsWith("Framework.", System.StringComparison.Ordinal))
                    {
                        Assert.IsTrue(graph.ContainsKey(reference), pair.Key + " -> " + reference);
                    }
                }
            }
        }

        static void AssertNoCycle(Dictionary<string, List<string>> graph)
        {
            var state = new Dictionary<string, int>();
            foreach (var name in graph.Keys)
            {
                Visit(name, graph, state);
            }
        }

        static void Visit(string name, Dictionary<string, List<string>> graph, Dictionary<string, int> state)
        {
            if (!graph.ContainsKey(name))
            {
                return;
            }

            if (state.TryGetValue(name, out var mark))
            {
                Assert.AreNotEqual(1, mark, "cycle at " + name);
                return;
            }

            state[name] = 1;
            var refs = graph[name];
            for (var i = 0; i < refs.Count; i++)
            {
                Visit(refs[i], graph, state);
            }

            state[name] = 2;
        }

        static string FindAssetsFolder()
        {
            try
            {
                if (!string.IsNullOrEmpty(UnityEngine.Application.dataPath) &&
                    Directory.Exists(UnityEngine.Application.dataPath))
                {
                    return UnityEngine.Application.dataPath;
                }
            }
            catch (System.Exception)
            {
                // 近似编译里的 Unity 桩可能没有 dataPath。
            }

            var current = Directory.GetCurrentDirectory();
            for (var i = 0; i < 6 && !string.IsNullOrEmpty(current); i++)
            {
                var candidate = Path.Combine(current, "Assets");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                current = Path.GetDirectoryName(current);
            }

            return "/workspace/Assets";
        }
    }
}
