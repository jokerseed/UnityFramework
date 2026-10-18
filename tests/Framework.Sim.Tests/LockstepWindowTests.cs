using System;
using Framework.Battle;
using Framework.GamePlay;
using Framework.Lockstep;
using NUnit.Framework;

namespace Framework.Sim.Tests
{
    /// <summary>锁步主机只接受窗口内的输入，并随窗口前移丢弃旧检查点。</summary>
    public sealed class LockstepWindowTests
    {
        [Test]
        public void LongMatch_KeepsCheckpointCountBounded()
        {
            var hub = new LoopbackHub();
            var linkA = hub.Open(1);
            var linkB = hub.Open(2);
            using (var simA = new BattleLockstepSimulation(7))
            using (var simB = new BattleLockstepSimulation(7))
            {
                var players = new[] { 1, 2 };
                var hostA = new LockstepHost(linkA, 1, players, 4, simA, BattleSimulationVersion.Current, maxRollbackTicks: 20);
                var hostB = new LockstepHost(linkB, 2, players, 4, simB, BattleSimulationVersion.Current, maxRollbackTicks: 20);
                for (var tick = 0; tick < 200; tick++)
                {
                    hostA.SubmitLocal(tick, BattleLockstepSimulation.EncodeVelocity(100, tick % 7));
                    hostB.SubmitLocal(tick, BattleLockstepSimulation.EncodeVelocity(-60, 0));
                    Assert.IsTrue(hostA.TryAdvance());
                    Assert.IsTrue(hostB.TryAdvance());
                }

                // 窗口 20 步、间隔 4 步：最多保留窗口内的 5 份，加上窗口下界前的 1 份和最新的 1 份。
                Assert.LessOrEqual(simA.CheckpointCount, 7);
                Assert.AreEqual(hostB.Checksums[199], hostA.Checksums[199]);
            }
        }

        [Test]
        public void LateInputOutsideRollbackWindow_IsRejectedAndRecentOnesStillRollBack()
        {
            var hub = new LoopbackHub();
            var linkA = hub.Open(1);
            var linkB = hub.Open(2);
            linkB.HoldOutgoing = true;
            using (var simA = new BattleLockstepSimulation(9))
            using (var simB = new BattleLockstepSimulation(9))
            {
                var players = new[] { 1, 2 };
                var hostA = new LockstepHost(linkA, 1, players, 4, simA, BattleSimulationVersion.Current, maxRollbackTicks: 8);
                var hostB = new LockstepHost(linkB, 2, players, 4, simB, BattleSimulationVersion.Current, maxRollbackTicks: 8);
                var rejected = 0;
                var oldestRejected = int.MaxValue;
                var newestRejected = -1;
                hostA.InputRejected += (playerId, tick) =>
                {
                    Assert.AreEqual(2, playerId);
                    rejected++;
                    oldestRejected = Math.Min(oldestRejected, tick);
                    newestRejected = Math.Max(newestRejected, tick);
                };

                var placeholder = BattleLockstepSimulation.EncodeVelocity(0, 0);
                for (var tick = 0; tick < 30; tick++)
                {
                    hostA.SubmitLocal(tick, BattleLockstepSimulation.EncodeVelocity(120, 0));
                    hostB.SubmitLocal(tick, BattleLockstepSimulation.EncodeVelocity(-150, 40));
                    Assert.IsTrue(hostB.TryAdvance());
                    Assert.IsTrue(hostA.PredictRemote(2, placeholder));
                    Assert.IsTrue(hostA.TryAdvance());
                }

                var before = hostA.Checksums[29];
                linkB.Flush();

                // 已确认到第 30 步、窗口 8 步：0..21 步太旧被拒，22..29 步照常回滚重放。
                Assert.AreEqual(22, rejected);
                Assert.AreEqual(0, oldestRejected);
                Assert.AreEqual(21, newestRejected);
                Assert.AreEqual(30, hostA.Checksums.Count);
                Assert.AreNotEqual(before, hostA.Checksums[29]);
            }
        }

        [Test]
        public void InputTooFarAhead_IsRejected()
        {
            var hub = new LoopbackHub();
            var linkA = hub.Open(1);
            var linkB = hub.Open(2);
            using (var simA = new BattleLockstepSimulation(3))
            {
                var hostA = new LockstepHost(linkA, 1, new[] { 1, 2 }, 4, simA, BattleSimulationVersion.Current, maxInputAheadTicks: 10);
                var rejectedTick = -1;
                hostA.InputRejected += (_, tick) => rejectedTick = tick;

                linkB.OpRaiseEvent(
                    LockstepHost.InputEventCode,
                    new LockstepInputMessage(10, 2, BattleLockstepSimulation.EncodeVelocity(1, 1), BattleSimulationVersion.Current),
                    true,
                    null);
                Assert.AreEqual(10, rejectedTick);

                linkB.OpRaiseEvent(
                    LockstepHost.InputEventCode,
                    new LockstepInputMessage(9, 2, BattleLockstepSimulation.EncodeVelocity(1, 1), BattleSimulationVersion.Current),
                    true,
                    null);
                Assert.AreEqual(10, rejectedTick);

                Assert.Throws<ArgumentOutOfRangeException>(() => hostA.SubmitLocal(10, null));
                Assert.Throws<ArgumentOutOfRangeException>(() => hostA.SubmitLocal(-1, null));
            }
        }
    }
}
