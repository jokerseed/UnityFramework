using System;
using System.Collections.Generic;
using System.Reflection;
using Framework.Lockstep;
using NUnit.Framework;

namespace Framework.Tests
{
    /// <summary>旧锁步网络包在长度不够时拒绝，而不是抛进模拟。</summary>
    public sealed class LockstepInputTests
    {
        [Test]
        public void ShortSyncedDataAndSyncedInfoAreRejected()
        {
            List<SyncedData> list;
            Assert.IsFalse(SyncedData.TryDecode(null, out list));
            Assert.IsFalse(SyncedData.TryDecode(new byte[3], out list));
            Assert.Throws<ArgumentException>(() => SyncedData.Decode(new byte[3]));

            SyncedInfo info;
            Assert.IsFalse(SyncedInfo.TryDecode(null, out info));
            Assert.IsFalse(SyncedInfo.TryDecode(new byte[10], out info));

            var encoded = SyncedInfo.Encode(new SyncedInfo
            {
                playerId = 2,
                tick = 4,
                checksum = new string('a', 32)
            });
            Assert.IsTrue(SyncedInfo.TryDecode(encoded, out info));
            Assert.AreEqual(2, info.playerId);
            Assert.AreEqual(4, info.tick);
            Assert.AreEqual(32, info.checksum.Length);
        }

        [Test]
        public void OneByteChecksumPacketDoesNotLatchFailure()
        {
            var hub = new LoopbackHub();
            var local = hub.Open(0);
            var remote = hub.Open(1);
            var step = AbstractLockstep.NewInstance(
                0.02f,
                local,
                null,
                1,
                1,
                0,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
            step.AddPlayer(0, "local", true);
            step.AddPlayer(1, "remote", false);

            var windowField = typeof(AbstractLockstep).GetField(
                "bufferSyncedInfo",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var window = windowField.GetValue(step);
            var buffer = (SyncedInfo[])window.GetType().GetField("buffer").GetValue(window);
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = new SyncedInfo { playerId = 0, tick = -1, checksum = new string('b', 32) };
            }

            buffer[0].tick = 0;
            var rejected = step.RejectedNetworkInputs;
            remote.OpRaiseEvent(198, new byte[] { 1 }, true, null);
            Assert.IsTrue(step.checksumOk);
            Assert.Greater(step.RejectedNetworkInputs, rejected);
        }

        [Test]
        public void ReadyInputClearsDropVotesAndTheNextGapStartsFromZero()
        {
            var hub = new LoopbackHub();
            var step = AbstractLockstep.NewInstance(
                0.02f,
                hub.Open(0),
                null,
                0,
                1,
                0,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                () => new EmptyInput());
            step.AddPlayer(0, "local", true);
            step.AddPlayer(1, "remote", false);
            step.AddPlayer(2, "other", false);
            var remote = step.ActivePlayers[1];
            var register = typeof(TSPlayer).GetMethod(
                "RegisterDropVote",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var drop = typeof(AbstractLockstep).GetMethod(
                "DropLagPlayers",
                BindingFlags.Instance | BindingFlags.NonPublic);

            drop.Invoke(step, null);
            Assert.AreEqual(1, remote.dropCount);
            Assert.IsTrue((bool)register.Invoke(remote, new object[] { (byte)2 }));

            var ready = remote.GetData(0);
            ready.fake = false;
            drop.Invoke(step, null);
            Assert.AreEqual(0, remote.dropCount);
            Assert.IsTrue((bool)register.Invoke(remote, new object[] { (byte)2 }));

            remote.RemoveData(0);
            drop.Invoke(step, null);
            Assert.AreEqual(1, remote.dropCount);
        }

        /// <summary>掉线票测试用的空输入，避免 <c>SyncedData.Init</c> 碰到空的输入对象。</summary>
        sealed class EmptyInput : InputDataBase
        {
            public override void Serialize(List<byte> bytes)
            {
            }

            public override void Deserialize(byte[] data, ref int offset)
            {
            }

            public override bool EqualsData(InputDataBase otherBase)
            {
                return true;
            }

            public override void CleanUp()
            {
            }

            public override void CopyFrom(InputDataBase fromBase)
            {
            }
        }
    }
}
