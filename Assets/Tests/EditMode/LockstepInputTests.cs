using System;
using System.Collections.Generic;
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
    }
}
