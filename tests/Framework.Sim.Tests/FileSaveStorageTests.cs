using System;
using System.IO;
using System.Text;
using Framework.Save;
using NUnit.Framework;

namespace Framework.Sim.Tests
{
    public sealed class FileSaveStorageTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "fw-save-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, true);
            }
        }

        [Test]
        public void SecondWrite_KeepsPreviousAsBackup()
        {
            var storage = new FileSaveStorage(_dir);
            storage.Write("slot", Encoding.UTF8.GetBytes("first"));
            storage.Write("slot", Encoding.UTF8.GetBytes("second"));

            Assert.IsTrue(storage.TryRead("slot", out var current));
            Assert.AreEqual("second", Encoding.UTF8.GetString(current));
            Assert.IsTrue(storage.TryReadBackup("slot", out var backup));
            Assert.AreEqual("first", Encoding.UTF8.GetString(backup));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LoadFromBackup_ThenSave_KeepsLastValidSaveRecoverable(bool missingPrimary)
        {
            var storage = new FileSaveStorage(_dir);
            storage.Write("slot", Encoding.UTF8.GetBytes("first"));
            storage.Write("slot", Encoding.UTF8.GetBytes("second"));
            if (missingPrimary) File.Delete(Path.Combine(_dir, "slot.sav"));
            else File.WriteAllText(Path.Combine(_dir, "slot.sav"), "corrupt");

            Assert.IsTrue(SaveReader.TryLoad(storage, Deserialize, "slot", out var loaded));
            Assert.AreEqual("first", loaded);
            Assert.AreEqual("first", File.ReadAllText(Path.Combine(_dir, "slot.sav")));
            Assert.AreEqual("first", File.ReadAllText(Path.Combine(_dir, "slot.sav.bak")));
            Assert.IsEmpty(Directory.GetFiles(_dir, "*.tmp"));
            // 模拟重启：保护不能只依赖 SaveManager / FileSaveStorage 实例里的标记。
            storage = new FileSaveStorage(_dir);
            storage.Write("slot", Encoding.UTF8.GetBytes("third"));
            Assert.IsTrue(storage.TryReadBackup("slot", out var backup));
            Assert.AreEqual("first", Encoding.UTF8.GetString(backup));

            File.WriteAllText(Path.Combine(_dir, "slot.sav"), "corrupt");
            Assert.IsTrue(SaveReader.TryLoad(storage, Deserialize, "slot", out loaded));
            Assert.AreEqual("first", loaded);
        }

        [Test]
        public void MissingPrimary_CorruptBackup_ThrowsInsteadOfReportingMissing()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "slot.sav.bak"), "corrupt");
            var storage = new FileSaveStorage(_dir);

            Assert.Throws<FormatException>(() => SaveReader.TryLoad(storage, Deserialize, "slot", out _));
            Assert.AreEqual("corrupt", File.ReadAllText(Path.Combine(_dir, "slot.sav.bak")));
            Assert.IsFalse(storage.Exists("slot"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RestoreBackup_PreservesExactBytes(bool missingPrimary)
        {
            var storage = new FileSaveStorage(_dir);
            var bytes = new byte[] { 0, 255, 128, 13, 10, 1 };
            storage.Write("slot", bytes);
            storage.Write("slot", Encoding.UTF8.GetBytes("corrupt"));
            if (missingPrimary) File.Delete(Path.Combine(_dir, "slot.sav"));

            ((ISaveRecoveryStorage)storage).RestoreBackup("slot", bytes);

            Assert.IsTrue(storage.TryRead("slot", out var primary));
            Assert.IsTrue(storage.TryReadBackup("slot", out var backup));
            CollectionAssert.AreEqual(bytes, primary);
            CollectionAssert.AreEqual(bytes, backup);
        }

        [Test]
        public void RecoveryTempWriteFailure_LeavesBothCopiesUntouchedAndAllowsRetry()
        {
            var storage = new FileSaveStorage(_dir);
            storage.Write("slot", Encoding.UTF8.GetBytes("first"));
            storage.Write("slot", Encoding.UTF8.GetBytes("corrupt"));
            var temp = Path.Combine(_dir, "slot.sav.tmp");
            Directory.CreateDirectory(temp);

            Assert.Throws<UnauthorizedAccessException>(() => SaveReader.TryLoad(storage, Deserialize, "slot", out _));
            Assert.AreEqual("corrupt", File.ReadAllText(Path.Combine(_dir, "slot.sav")));
            Assert.AreEqual("first", File.ReadAllText(Path.Combine(_dir, "slot.sav.bak")));

            Directory.Delete(temp);
            Assert.IsTrue(SaveReader.TryLoad(storage, Deserialize, "slot", out var loaded));
            Assert.AreEqual("first", loaded);
            Assert.AreEqual("first", File.ReadAllText(Path.Combine(_dir, "slot.sav")));
        }

        [Test]
        public void RecoveryPromotionFailure_LeavesBackupUntouchedAndAllowsRetry()
        {
            var storage = new FileSaveStorage(_dir);
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "slot.sav.bak"), "first");
            var primary = Path.Combine(_dir, "slot.sav");
            Directory.CreateDirectory(primary); // 临时文件写完后，不能提升到同名目录。

            Assert.Throws<IOException>(() => storage.RestoreBackup("slot", Encoding.UTF8.GetBytes("first")));
            Assert.AreEqual("first", File.ReadAllText(Path.Combine(_dir, "slot.sav.bak")));
            Assert.IsTrue(Directory.Exists(primary));

            Directory.Delete(primary);
            Assert.IsTrue(SaveReader.TryLoad(storage, Deserialize, "slot", out var loaded));
            Assert.AreEqual("first", loaded);
            Assert.AreEqual("first", File.ReadAllText(primary));
        }

        [Test]
        public void BothCopiesCorrupt_ThrowsAndDoesNotRewriteEitherCopy()
        {
            var storage = new FileSaveStorage(_dir);
            storage.Write("slot", Encoding.UTF8.GetBytes("corrupt"));
            storage.Write("slot", Encoding.UTF8.GetBytes("corrupt"));

            Assert.Throws<FormatException>(() => SaveReader.TryLoad(storage, Deserialize, "slot", out _));
            Assert.AreEqual("corrupt", File.ReadAllText(Path.Combine(_dir, "slot.sav")));
            Assert.AreEqual("corrupt", File.ReadAllText(Path.Combine(_dir, "slot.sav.bak")));
            Assert.IsEmpty(Directory.GetFiles(_dir, "*.tmp"));
        }

        [Test]
        public void NoCopies_ReturnsFalseWithoutCreatingDirectory()
        {
            var storage = new FileSaveStorage(_dir);

            Assert.IsFalse(SaveReader.TryLoad(storage, Deserialize, "slot", out var loaded));
            Assert.IsNull(loaded);
            Assert.IsFalse(Directory.Exists(_dir));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DirectoryAtSavePath_IsNotReportedAsMissing(bool backup)
        {
            var storage = new FileSaveStorage(_dir);
            Directory.CreateDirectory(Path.Combine(_dir, backup ? "slot.sav.bak" : "slot.sav"));

            Assert.Throws<UnauthorizedAccessException>(() => SaveReader.TryLoad(storage, Deserialize, "slot", out _));
        }

        static string Deserialize(byte[] data)
        {
            var value = Encoding.UTF8.GetString(data);
            if (value == "corrupt") throw new FormatException("Invalid test save.");
            return value;
        }

        [Test]
        public void ListKeys_IgnoresBackupAndTempFiles()
        {
            var storage = new FileSaveStorage(_dir);
            storage.Write("a", new byte[] { 1 });
            storage.Write("a", new byte[] { 2 });
            File.WriteAllBytes(Path.Combine(_dir, "b.sav.tmp"), new byte[] { 3 });

            CollectionAssert.AreEqual(new[] { "a" }, storage.ListKeys());
        }

        [Test]
        public void Delete_RemovesBackupToo()
        {
            var storage = new FileSaveStorage(_dir);
            storage.Write("slot", new byte[] { 1 });
            storage.Write("slot", new byte[] { 2 });

            Assert.IsTrue(storage.Delete("slot"));
            Assert.IsFalse(storage.TryRead("slot", out _));
            Assert.IsFalse(storage.TryReadBackup("slot", out _));
        }
    }
}
