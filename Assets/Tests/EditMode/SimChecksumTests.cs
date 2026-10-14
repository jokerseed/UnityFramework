using System.Collections.Generic;
using BtTree = Framework.BehaviourTree.BehaviourTree;
using Framework.BehaviourTree;
using Framework.Battle;
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

            var tree = new BtTree(BtTreeBuilder.Action(_ => BtStatus.Success));
            var left = new BattleAgent(tree, new BtBlackboard(), sessionSeed: 42, actorValue: 3);
            var right = new BattleAgent(tree.Instantiate(), new BtBlackboard(), sessionSeed: 42, actorValue: 4);
            Assert.AreNotEqual(left.Random.GetStateHash(), right.Random.GetStateHash());
        }

        [Test]
        public void BehaviourTreeSnapshotRestoresRandomAndClearsObjectBag()
        {
            var tree = new BtTree(BtTreeBuilder.Action(_ => BtStatus.Success));
            var random = TSRandom.New(9);
            var board = new BtBlackboard();
            var context = new BtContext(board, random: random);
            var snapshot = tree.CaptureSnapshot(context);
            var expected = random.Next();
            random.Next();
            board.SetObject("marker", new object());
            tree.RestoreSnapshot(snapshot, context);
            Assert.AreEqual(expected, random.Next());
            Assert.IsFalse(board.TryGet("marker", out object _));
        }

        [Test]
        public void ChecksumChangesWhenAgentBlackboardChanges()
        {
            using (var framework = new GamePlayFramework(7))
            {
                var actorId = new ActorId(1);
                framework.CreateActor(actorId, Vector3.zero, 100f, teamId: 0);
                var before = BattleFrameChecksum.Compute(framework);
                var tree = new BtTree(BtTreeBuilder.Action(_ => BtStatus.Success));
                var agent = new BattleAgent(tree, new BtBlackboard(), random: TSRandom.New(3));
                framework.SetBattleAgent(actorId, agent);
                var withAgent = BattleFrameChecksum.Compute(framework);
                Assert.AreNotEqual(before, withAgent);

                agent.Blackboard.Set("flag", 1);
                var withKey = BattleFrameChecksum.Compute(framework);
                Assert.AreNotEqual(withAgent, withKey);

                agent.Blackboard.SetString(BattleAiBlackboardKeys.AbilityId, "MobSlash");
                var withAbility = BattleFrameChecksum.Compute(framework);
                Assert.AreNotEqual(withKey, withAbility);
            }
        }

        [Test]
        public void AgentRollbackRestoresAbilityIdAndDropsObjectBag()
        {
            var tree = new BtTree(BtTreeBuilder.Action(_ => BtStatus.Success));
            var board = new BtBlackboard();
            board.SetString(BattleAiBlackboardKeys.AbilityId, "MobSlash");
            board.SetObject("marker", new object());
            var agent = new BattleAgent(tree, board, sessionSeed: 4, actorValue: 9);
            var snapshot = agent.CaptureTree();
            board.SetString(BattleAiBlackboardKeys.AbilityId, "Other");
            board.SetObject("marker", new object());
            agent.Random.Next();
            agent.RestoreTree(snapshot);

            Assert.IsTrue(board.TryGet(BattleAiBlackboardKeys.AbilityId, out string ability));
            Assert.AreEqual("MobSlash", ability);
            Assert.IsFalse(board.TryGet("marker", out object _));
        }

        [Test]
        public void SetObjectRoutesStringsAndClearsTheStringSlot()
        {
            var board = new BtBlackboard();
            board.SetString("k", "a");
            board.SetObject("k", "b");
            Assert.IsTrue(board.TryGet("k", out string text));
            Assert.AreEqual("b", text);

            board.SetObject("k", new object());
            Assert.IsFalse(board.TryGet("k", out string _));
            Assert.IsTrue(board.TryGet("k", out object _));
        }

        [Test]
        public void ReplayWithAgentMatchesRecordedChecksums()
        {
            var frames = new List<BattleReplayFrame>();
            using (var director = new BattleDirector())
            {
                var session = director.CreateSession(11);
                var id = session.AllocateActorId();
                session.Framework.CreateActor(id, Vector3.zero, 80f, 1);
                var tree = new BtTree(BtTreeBuilder.Action(_ => BtStatus.Success));
                session.Framework.SetBattleAgent(
                    id,
                    new BattleAgent(tree, new BtBlackboard(), sessionSeed: 11, actorValue: id.Value));
                for (var i = 0; i < 3; i++)
                {
                    BattleIntentCommand[] recorded = null;
                    session.Step(1, _ =>
                    {
                        var commands = new List<BattleIntentCommand>();
                        session.Framework.CollectAiIntents(commands, session.FixedDeltaTime);
                        recorded = commands.ToArray();
                        BattleIntentApplier.ApplyAll(session.Framework, recorded);
                    });
                    frames.Add(new BattleReplayFrame(i, recorded, BattleFrameChecksum.Compute(session.Framework)));
                }
            }

            using (var director = new BattleDirector())
            {
                var session = director.CreateSession(11);
                var id = session.AllocateActorId();
                session.Framework.CreateActor(id, Vector3.zero, 80f, 1);
                var tree = new BtTree(BtTreeBuilder.Action(_ => BtStatus.Success));
                session.Framework.SetBattleAgent(
                    id,
                    new BattleAgent(tree, new BtBlackboard(), sessionSeed: 11, actorValue: id.Value));
                var tape = new BattleReplayTape(11, session.FixedDeltaTime, frames);
                var result = BattleReplayVerifier.Run(tape, session);
                Assert.IsTrue(result.Matched, "mismatch frame " + result.MismatchFrameIndex);
            }
        }
    }
}
