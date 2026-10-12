using Framework.BehaviourTree;
using Framework.Core;
using Framework.FixedMath;
using Framework.GamePlay;
using NUnit.Framework;
using UnityEngine;

namespace Framework.Tests
{
    /// <summary>Agent 种子、行为树随机快照，以及校验和对 AI 状态敏感。</summary>
    public sealed class SimChecksumTests
    {
        [Test]
        public void NullRandomDerivesFromSessionAndActor()
        {
            Assert.AreNotEqual(1, BattleAgent.DeriveSeed(1, 7));
            Assert.AreEqual(BattleAgent.DeriveSeed(42, 3), BattleAgent.DeriveSeed(42, 3));
            Assert.AreNotEqual(BattleAgent.DeriveSeed(42, 3), BattleAgent.DeriveSeed(42, 4));
            Assert.AreNotEqual(0, BattleAgent.DeriveSeed(unchecked((int)0x9E3779B9), 0));

            var tree = new BehaviourTree(BtTreeBuilder.Action(_ => BtStatus.Success));
            var left = new BattleAgent(tree, new BtBlackboard(), sessionSeed: 42, actorValue: 3);
            var right = new BattleAgent(tree.Instantiate(), new BtBlackboard(), sessionSeed: 42, actorValue: 4);
            Assert.AreNotEqual(left.Random.GetStateHash(), right.Random.GetStateHash());
        }

        [Test]
        public void BehaviourTreeSnapshotRestoresRandomAndClearsObjectBag()
        {
            var tree = new BehaviourTree(BtTreeBuilder.Action(_ => BtStatus.Success));
            var random = TSRandom.New(9);
            var board = new BtBlackboard();
            var context = new BtContext(board, random: random);
            var expected = random.Next();
            var snapshot = tree.CaptureSnapshot(context);
            random.Next();
            board.SetObject("marker", "later");
            tree.RestoreSnapshot(snapshot, context);
            Assert.AreEqual(expected, random.Next());
            Assert.IsFalse(board.TryGet("marker", out string _));
        }

        [Test]
        public void ChecksumChangesWhenAgentBlackboardChanges()
        {
            using (var framework = new GamePlayFramework(7))
            {
                var actorId = new ActorId(1);
                framework.CreateActor(actorId, Vector3.zero, 100f, teamId: 0);
                var before = BattleFrameChecksum.Compute(framework);
                var tree = new BehaviourTree(BtTreeBuilder.Action(_ => BtStatus.Success));
                var agent = new BattleAgent(tree, new BtBlackboard(), random: TSRandom.New(3));
                framework.SetBattleAgent(actorId, agent);
                var withAgent = BattleFrameChecksum.Compute(framework);
                Assert.AreNotEqual(before, withAgent);

                agent.Blackboard.Set("flag", 1);
                var withKey = BattleFrameChecksum.Compute(framework);
                Assert.AreNotEqual(withAgent, withKey);
            }
        }
    }
}
