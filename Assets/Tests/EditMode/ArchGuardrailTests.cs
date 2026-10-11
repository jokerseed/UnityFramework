using System.Collections.Generic;
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
    }
}
