using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Framework.BehaviourTree;
using Framework.Battle;
using Framework.Editor.Determinism;
using Framework.Events;
using Framework.FixedMath;
using Framework.GAS;
using Framework.GAS.Combat;
using NUnit.Framework;

namespace Framework.Tests
{
    /// <summary>扫描器范围、普通伤害结算和固定帧等待。不改模拟公式。</summary>
    public sealed class ArchGuardrailTests
    {
        [Test]
        public void ScannerRejectsFloatInBehaviourTreeRuntime()
        {
            var violations = new List<string>();
            SimDeterminismScanner.CollectViolations(
                "Packages/com.jokerseed.framework.battle/Runtime/BehaviourTree/BtSample.cs",
                "float speed = 1f;",
                violations);
            Assert.Greater(violations.Count, 0);
        }

        [Test]
        public void ScannerIgnoresFloatInBehaviourTreeConfig()
        {
            var violations = new List<string>();
            SimDeterminismScanner.CollectViolations(
                "Packages/com.jokerseed.framework.battle/Runtime/BehaviourTree/Config/BtConfigNode.cs",
                "public float FloatValue;",
                violations);
            Assert.IsEmpty(violations);
        }

        [Test]
        public void SimAssembliesDoNotReferenceUnityEngine()
        {
            var projectRoot = Path.GetDirectoryName(FindAssetsFolder());
            var battle = Path.Combine(projectRoot, "Packages", "com.jokerseed.framework.battle", "Runtime");
            string[] simAsmdefs =
            {
                Path.Combine(battle, "FixedMath", "Framework.FixedMath.asmdef"),
                Path.Combine(battle, "Lockstep", "Framework.Lockstep.asmdef"),
                Path.Combine(battle, "LockstepPhysics", "Framework.LockstepPhysics.asmdef"),
                Path.Combine(battle, "Battle.Core", "Framework.Battle.Core.asmdef"),
                Path.Combine(battle, "ECS", "Framework.ECS.asmdef"),
                Path.Combine(battle, "ECS.Physics", "Framework.ECS.Physics.asmdef"),
                Path.Combine(battle, "GAS", "Framework.GAS.asmdef"),
                Path.Combine(battle, "BehaviourTree", "Framework.BehaviourTree.asmdef"),
                Path.Combine(battle, "GamePlay", "Framework.GamePlay.asmdef"),
            };
            for (var i = 0; i < simAsmdefs.Length; i++)
            {
                var text = File.ReadAllText(simAsmdefs[i]);
                Assert.IsTrue(
                    Regex.IsMatch(text, "\"noEngineReferences\"\\s*:\\s*true"),
                    Path.GetFileName(simAsmdefs[i]) + " 必须开启 noEngineReferences，模拟层不能直接用 UnityEngine。");
            }
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
            var projectRoot = Path.GetDirectoryName(FindAssetsFolder());
            var graph = new Dictionary<string, List<string>>();
            var asmdefs = new List<string>();
            asmdefs.AddRange(Directory.GetFiles(Path.Combine(projectRoot, "Assets"), "*.asmdef", SearchOption.AllDirectories));
            asmdefs.AddRange(Directory.GetFiles(Path.Combine(projectRoot, "Packages"), "*.asmdef", SearchOption.AllDirectories));
            foreach (var file in asmdefs)
            {
                var text = File.ReadAllText(file);
                var name = Regex.Match(text, "\"name\"\\s*:\\s*\"([^\"]+)\"").Groups[1].Value;
                if (!name.StartsWith("Framework.", System.StringComparison.Ordinal)
                    && name != "Game"
                    && !name.StartsWith("Game.", System.StringComparison.Ordinal))
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

            Assert.IsTrue(graph.ContainsKey("Game"));
            Assert.IsFalse(graph["Framework.Core"].Contains("Framework.Events"));
            Assert.IsFalse(graph["Framework.Core"].Contains("Framework.FixedMath"), "Core 不应把定点数学带给不做帧同步的项目。");
            Assert.IsFalse(graph["Framework.GamePlay"].Contains("Generated.Luban"));
            Assert.IsTrue(graph["Game"].Contains("Game.Luban"));
            Assert.IsTrue(graph["Game.Luban"].Contains("Luban.Runtime"));
            Assert.IsTrue(graph["Framework.Network"].Contains("KCP"));
            Assert.IsTrue(graph["Framework.ECS.Physics"].Contains("Framework.LockstepPhysics"));
            // GUID 形式的引用（例如 Generated.Luban → Luban.Runtime）不解析成程序集名，环检测只覆盖按名字写出的引用。
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

        [Test]
        public void ScannerRootsCoverSplitSimAssemblies()
        {
            var roots = new List<string>(SimDeterminismScanner.ScanRoots);
            var battle = SimDeterminismScanner.BattleRuntimeRoot;
            string[] required =
            {
                battle + "/ECS",
                battle + "/ECS.Physics",
                battle + "/GAS",
                battle + "/FixedMath",
                battle + "/GamePlay",
                battle + "/BehaviourTree",
                battle + "/Battle.Core",
                battle + "/Lockstep",
                "Assets/Scripts/Luban",
            };
            for (var i = 0; i < required.Length; i++)
            {
                Assert.IsTrue(roots.Contains(required[i]), required[i]);
            }

            Assert.IsFalse(roots.Contains(battle + "/LockstepPhysics"));
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

            var starts = new List<string>
            {
                Directory.GetCurrentDirectory(),
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            };
            for (var s = 0; s < starts.Count; s++)
            {
                var current = starts[s];
                for (var i = 0; i < 8 && !string.IsNullOrEmpty(current); i++)
                {
                    var candidate = Path.Combine(current, "Assets");
                    var coreAsmdef = Path.Combine(current, "Packages", "com.jokerseed.framework.core", "Runtime", "Core", "Framework.Core.asmdef");
                    if (Directory.Exists(candidate) && File.Exists(coreAsmdef))
                    {
                        return candidate;
                    }

                    current = Path.GetDirectoryName(current);
                }
            }

            Assert.Inconclusive("找不到含 Packages/com.jokerseed.framework.core 的工程目录。");
            return null;
        }
    }
}
