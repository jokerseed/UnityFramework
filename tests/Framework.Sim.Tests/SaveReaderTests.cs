using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Framework.Save;
using NUnit.Framework;

namespace Framework.Sim.Tests
{
    public sealed class SaveReaderTests
    {
        [TestCase(null, null)]
        [TestCase(null, "bad")]
        [TestCase("bad", null)]
        [TestCase("bad", "bad")]
        public void NoUsableCopy_ReturnsFalseOnlyWhenBothAreAbsent(string primary, string backup)
        {
            var storage = new BackupStorage { Primary = primary, Backup = backup };
            if (primary == null && backup == null)
            {
                Assert.IsFalse(SaveReader.TryLoad(storage, Deserialize, "slot", out var value));
                Assert.IsNull(value);
            }
            else
            {
                Assert.Throws<FormatException>(() => SaveReader.TryLoad(storage, Deserialize, "slot", out _));
            }
        }

        [Test]
        public void MissingPrimary_BackupReadFailure_IsNotMissing()
        {
            var error = new IOException("Backup read failed.");
            var storage = new BackupStorage { BackupError = error };

            var thrown = Assert.Throws<FormatException>(() => SaveReader.TryLoad(storage, Deserialize, "slot", out _));

            Assert.AreSame(error, thrown.InnerException);
            Assert.AreEqual(1, storage.PrimaryReads);
            Assert.AreEqual(1, storage.BackupReads);
        }

        [Test]
        public void BothReadsFail_PreservesBothErrors()
        {
            var storage = new BackupStorage
            {
                PrimaryError = new IOException("Primary read failed."),
                BackupError = new IOException("Backup read failed.")
            };

            var thrown = Assert.Throws<FormatException>(() => SaveReader.TryLoad(storage, Deserialize, "slot", out _));

            var errors = (AggregateException)thrown.InnerException;
            CollectionAssert.AreEqual(new[] { storage.PrimaryError, storage.BackupError }, errors.InnerExceptions);
        }

        [TestCase(null)]
        [TestCase("bad")]
        public void BackupOnlyStorage_RemainsCompatible(string primary)
        {
            var storage = new BackupStorage { Primary = primary, Backup = "backup" };

            Assert.IsTrue(SaveReader.TryLoad(storage, Deserialize, "slot", out var value));

            Assert.AreEqual("backup", value);
            Assert.AreEqual(primary, storage.Primary);
            Assert.AreEqual(0, storage.Writes);
        }

        [TestCase(null)]
        [TestCase("bad")]
        public void RecoveryFailure_PropagatesOnceWithoutOrdinaryWrite(string primary)
        {
            var storage = new RecoveryStorage
            {
                Primary = primary,
                Backup = "backup",
                RecoveryError = new IOException("Promotion failed.")
            };

            var thrown = Assert.Throws<IOException>(() => SaveReader.TryLoad(storage, Deserialize, "slot", out _));

            Assert.AreSame(storage.RecoveryError, thrown);
            Assert.AreEqual(1, storage.PrimaryReads);
            Assert.AreEqual(1, storage.BackupReads);
            Assert.AreEqual(1, storage.Recoveries);
            Assert.AreEqual(0, storage.Writes);
            Assert.AreEqual("backup", storage.Backup);
        }

        [Test]
        public void ValidPrimary_DoesNotReadOrRestoreBackup()
        {
            var storage = new RecoveryStorage
            {
                Primary = "primary",
                BackupError = new IOException("Must not read backup."),
                RecoveryError = new IOException("Must not restore backup.")
            };

            Assert.IsTrue(SaveReader.TryLoad(storage, Deserialize, "slot", out var value));

            Assert.AreEqual("primary", value);
            Assert.AreEqual(0, storage.BackupReads);
            Assert.AreEqual(0, storage.Recoveries);
            Assert.AreEqual(0, storage.Writes);
        }

        [TestCase(null)]
        [TestCase("bad")]
        public void InvalidBackup_IsNeverPromoted(string primary)
        {
            var storage = new RecoveryStorage { Primary = primary, Backup = "bad" };

            Assert.Throws<FormatException>(() => SaveReader.TryLoad(storage, Deserialize, "slot", out _));

            Assert.AreEqual(0, storage.Recoveries);
            Assert.AreEqual(0, storage.Writes);
        }

        [Test]
        public void PrimaryReadFailure_ValidBackupIsPromoted()
        {
            var storage = new RecoveryStorage { PrimaryError = new IOException("Primary unavailable."), Backup = "backup" };

            Assert.IsTrue(SaveReader.TryLoad(storage, Deserialize, "slot", out var value));

            Assert.AreEqual("backup", value);
            Assert.AreEqual("backup", storage.Primary);
            Assert.AreEqual("backup", storage.Backup);
            Assert.AreEqual(1, storage.Recoveries);
            Assert.AreEqual(0, storage.Writes);
        }

        static string Deserialize(byte[] data)
        {
            var text = Encoding.UTF8.GetString(data);
            if (text == "bad") throw new FormatException("Invalid test save.");
            return text;
        }

        class BackupStorage : ISaveStorage, ISaveBackupStorage
        {
            public string Primary { get; set; }
            public string Backup { get; set; }
            public Exception PrimaryError { get; set; }
            public Exception BackupError { get; set; }
            public int PrimaryReads { get; private set; }
            public int BackupReads { get; private set; }
            public int Writes { get; private set; }

            public bool Exists(string key) => Primary != null;
            public bool TryRead(string key, out byte[] data)
            {
                PrimaryReads++;
                if (PrimaryError != null) throw PrimaryError;
                data = Primary == null ? null : Encoding.UTF8.GetBytes(Primary);
                return data != null;
            }
            public bool TryReadBackup(string key, out byte[] data)
            {
                BackupReads++;
                if (BackupError != null) throw BackupError;
                data = Backup == null ? null : Encoding.UTF8.GetBytes(Backup);
                return data != null;
            }
            public void Write(string key, byte[] data)
            {
                Writes++;
                Backup = Primary;
                Primary = Encoding.UTF8.GetString(data);
            }
            public bool Delete(string key) => throw new NotSupportedException();
            public IReadOnlyList<string> ListKeys() => throw new NotSupportedException();
        }

        sealed class RecoveryStorage : BackupStorage, ISaveRecoveryStorage
        {
            public Exception RecoveryError { get; set; }
            public int Recoveries { get; private set; }
            public void RestoreBackup(string key, byte[] data)
            {
                Recoveries++;
                if (RecoveryError != null) throw RecoveryError;
                Primary = Encoding.UTF8.GetString(data);
            }
        }
    }
}
