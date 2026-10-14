using Framework.GamePlay;
using Framework.Lockstep;
using NUnit.Framework;

namespace Framework.Tests
{
    /// <summary>两个内存对端在等输入和迟到回滚之后，逐步校验和相同。</summary>
    public sealed class LockstepRollbackTests
    {
        [Test]
        public void WaitForInputPeersMatchChecksums()
        {
            var hub = new LoopbackHub();
            var linkA = hub.Open(1);
            var linkB = hub.Open(2);
            using (var simA = new BattleLockstepSimulation(42))
            using (var simB = new BattleLockstepSimulation(42))
            {
                var hostA = new LockstepHost(linkA, 1, new[] { 1, 2 }, 4, simA, BattleSimulationVersion.Current);
                var hostB = new LockstepHost(linkB, 2, new[] { 2, 1 }, 4, simB, BattleSimulationVersion.Current);
                for (var tick = 0; tick < 12; tick++)
                {
                    hostA.SubmitLocal(tick, BattleLockstepSimulation.EncodeVelocity(100 + tick, 0));
                    Assert.IsFalse(hostA.TryAdvance());
                    hostB.SubmitLocal(tick, BattleLockstepSimulation.EncodeVelocity(-80, tick));
                    Assert.IsTrue(hostA.TryAdvance());
                    Assert.IsTrue(hostB.TryAdvance());
                }

                Assert.AreEqual(12, hostA.Checksums.Count);
                Assert.AreNotEqual(hostA.Checksums[0], hostA.Checksums[11]);
                for (var i = 0; i < hostA.Checksums.Count; i++)
                {
                    Assert.AreEqual(hostA.Checksums[i], hostB.Checksums[i], "step " + i);
                }
            }
        }

        [Test]
        public void LateInputRollbackConverges()
        {
            var hub = new LoopbackHub();
            var linkA = hub.Open(1);
            var linkB = hub.Open(2);
            linkB.HoldOutgoing = true;
            using (var simA = new BattleLockstepSimulation(42))
            using (var simB = new BattleLockstepSimulation(42))
            {
                var players = new[] { 1, 2 };
                var hostA = new LockstepHost(linkA, 1, players, 4, simA, BattleSimulationVersion.Current);
                var hostB = new LockstepHost(linkB, 2, players, 4, simB, BattleSimulationVersion.Current);
                var placeholder = BattleLockstepSimulation.EncodeVelocity(0, 0);
                for (var tick = 0; tick < 12; tick++)
                {
                    hostA.SubmitLocal(tick, BattleLockstepSimulation.EncodeVelocity(120, 0));
                    hostB.SubmitLocal(tick, BattleLockstepSimulation.EncodeVelocity(-150, 40));
                    Assert.IsTrue(hostB.TryAdvance());
                    Assert.IsFalse(hostA.TryAdvance());
                    Assert.IsTrue(hostA.PredictRemote(2, placeholder));
                    Assert.IsTrue(hostA.TryAdvance());
                }

                Assert.AreNotEqual(hostA.Checksums[11], hostB.Checksums[11]);
                linkB.Flush();
                Assert.AreEqual(hostB.Checksums.Count, hostA.Checksums.Count);
                for (var i = 0; i < hostB.Checksums.Count; i++)
                {
                    Assert.AreEqual(hostB.Checksums[i], hostA.Checksums[i], "step " + i);
                }
            }
        }

        [Test]
        public void SimulationVersionMismatchStopsTheMatch()
        {
            var hub = new LoopbackHub();
            var linkA = hub.Open(1);
            var linkB = hub.Open(2);
            using (var simA = new BattleLockstepSimulation(42))
            using (var simB = new BattleLockstepSimulation(42))
            {
                var players = new[] { 1, 2 };
                var hostA = new LockstepHost(linkA, 1, players, 4, simA, BattleSimulationVersion.Current);
                var hostB = new LockstepHost(linkB, 2, players, 4, simB, BattleSimulationVersion.Current + 1);
                var reported = new System.Collections.Generic.List<(int Player, int Version)>();
                hostA.SimulationVersionMismatch += (player, version) => reported.Add((player, version));

                hostB.SubmitLocal(0, BattleLockstepSimulation.EncodeVelocity(1, 1));
                hostB.SubmitLocal(1, BattleLockstepSimulation.EncodeVelocity(1, 1));
                hostA.SubmitLocal(0, BattleLockstepSimulation.EncodeVelocity(1, 1));

                Assert.IsTrue(hostA.HasVersionMismatch);
                Assert.AreEqual(1, reported.Count, "reported once");
                Assert.AreEqual((2, BattleSimulationVersion.Current + 1), reported[0]);
                Assert.Throws<System.InvalidOperationException>(() => hostA.TryAdvance());
                Assert.Throws<System.InvalidOperationException>(() => hostB.TryAdvance());
            }
        }
    }
}
