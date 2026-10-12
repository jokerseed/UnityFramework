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
    }
}
