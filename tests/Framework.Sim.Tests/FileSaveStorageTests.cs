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

        [TestCase("first")]
        [TestCase("corrupt")]
        public void BackupOnlySlot_ExistsWithoutValidatingOrRestoring(string contents)
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "slot.sav.bak"), contents);
            var storage = new FileSaveStorage(_dir);
            var exists = storage.Exists("slot");
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "slot.sav")), "查询不能恢复或修改存档。");

            if (contents == "corrupt")
                Assert.Throws<FormatException>(() => SaveReader.TryLoad(storage, Deserialize, "slot", out _));
            else
            {
                Assert.IsTrue(SaveReader.TryLoad(storage, Deserialize, "slot", out var loaded));
                Assert.AreEqual(contents, loaded);
            }

            Assert.IsTrue(exists, "有效与损坏的仅备份都属于已存在的存档，不等于内容可读取。");
        }

        [TestCase("first")]
        [TestCase("corrupt")]
        public void BackupOnlySlot_IsListedWithoutRestoring(string contents)
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "slot.sav.bak"), contents);
            var storage = new FileSaveStorage(_dir);

            CollectionAssert.AreEqual(new[] { "slot" }, storage.ListKeys());
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "slot.sav")));
            Assert.AreEqual(contents, File.ReadAllText(Path.Combine(_dir, "slot.sav.bak")));
        }

        [TestCase("first")]
        [TestCase("corrupt")]
        public void BackupOnlySlot_DeleteReportsRemoval(string contents)
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "slot.sav.bak"), contents);
            var storage = new FileSaveStorage(_dir);

            var deleted = storage.Delete("slot");

            Assert.IsFalse(File.Exists(Path.Combine(_dir, "slot.sav.bak")));
            Assert.IsTrue(deleted, "实际删除了备份，必须报告删除成功。");
            Assert.IsFalse(storage.Delete("slot"));
            Assert.IsFalse(storage.Exists("slot"));
            Assert.IsEmpty(storage.ListKeys());
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
            Assert.IsTrue(storage.Exists("slot"), "损坏不是缺失。");
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "slot.sav")), "解析失败不能创建主档。");
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
        public void ListKeys_DeduplicatesPrimaryAndBackupAndIgnoresTempFiles()
        {
            var storage = new FileSaveStorage(_dir);
            storage.Write("a", new byte[] { 1 });
            storage.Write("a", new byte[] { 2 });
            File.WriteAllBytes(Path.Combine(_dir, "b.sav.tmp"), new byte[] { 3 });

            CollectionAssert.AreEqual(new[] { "a" }, storage.ListKeys());
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        [TestCase(false, true)]
        public void SlotLifecycle_UsesEitherCopyForDiscoveryAndDeletion(bool primary, bool backup)
        {
            var storage = new FileSaveStorage(_dir);
            if (primary || backup) Directory.CreateDirectory(_dir);
            if (primary) File.WriteAllText(Path.Combine(_dir, "slot.sav"), "primary");
            if (backup) File.WriteAllText(Path.Combine(_dir, "slot.sav.bak"), "backup");

            Assert.AreEqual(primary || backup, storage.Exists("slot"));
            CollectionAssert.AreEqual(primary || backup ? new[] { "slot" } : Array.Empty<string>(), storage.ListKeys());
            Assert.AreEqual(primary || backup, storage.Delete("slot"));
            Assert.IsFalse(storage.Exists("slot"));
            Assert.IsEmpty(storage.ListKeys());
            Assert.IsFalse(storage.Delete("slot"));
            Assert.IsFalse(storage.TryRead("slot", out _));
            Assert.IsFalse(storage.TryReadBackup("slot", out _));
        }

        [Test]
        public void ListKeys_MergesSortedKeysAndStripsOnlyTheFullSuffix()
        {
            var storage = new FileSaveStorage(_dir);
            Directory.CreateDirectory(_dir);
            foreach (var name in new[] { "z.sav.bak", "a.sav", "a.sav.bak", "slot.sav.sav.bak" })
                File.WriteAllText(Path.Combine(_dir, name), "corrupt");
            foreach (var name in new[] { "temp.sav.tmp", "extra.sav.bak.tmp", "extra.sav.bak.extra", "extra.sav.extra" })
                File.WriteAllText(Path.Combine(_dir, name), "ignored");

            CollectionAssert.AreEqual(new[] { "a", "slot.sav", "z" }, storage.ListKeys());
            Assert.IsTrue(storage.Exists("slot.sav"));
        }

        [Test]
        public void ListKeys_PreservesFilenameCaseAndPlatformWildcardMatching()
        {
            var storage = new FileSaveStorage(_dir);
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "A.sav"), "primary");
            File.WriteAllText(Path.Combine(_dir, "a.sav.bak"), "backup");
            File.WriteAllText(Path.Combine(_dir, "upper.SAV.BAK"), "backup");
            // 通配匹配规则不一定等同于卷的大小写查找规则（例如 macOS）。
            var suffixMatches = Array.Exists(Directory.GetFiles(_dir, "*.sav.bak"),
                path => Path.GetFileName(path) == "upper.SAV.BAK");

            CollectionAssert.AreEqual(suffixMatches ? new[] { "A", "a", "upper" } : new[] { "A", "a" }, storage.ListKeys());
        }

        [Test]
        public void TemporaryFileAlone_IsNotAStoredSlot()
        {
            var storage = new FileSaveStorage(_dir);
            Directory.CreateDirectory(_dir);
            var temp = Path.Combine(_dir, "slot.sav.tmp");
            File.WriteAllText(temp, "unfinished");

            Assert.IsFalse(storage.Exists("slot"));
            Assert.IsEmpty(storage.ListKeys());
            Assert.IsFalse(storage.Delete("slot"));
            Assert.AreEqual("unfinished", File.ReadAllText(temp));
        }

        [Test]
        public void Delete_InvalidBackupPath_ThrowsBeforeRemovingPrimary()
        {
            var storage = new FileSaveStorage(_dir);
            storage.Write("slot", Encoding.UTF8.GetBytes("primary"));
            Directory.CreateDirectory(Path.Combine(_dir, "slot.sav.bak"));

            Assert.Throws<IOException>(() => storage.Delete("slot"));
            Assert.AreEqual("primary", File.ReadAllText(Path.Combine(_dir, "slot.sav")));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Exists_DirectoryAtSavePath_IsAnErrorNotMissing(bool backup)
        {
            var storage = new FileSaveStorage(_dir);
            Directory.CreateDirectory(Path.Combine(_dir, backup ? "slot.sav.bak" : "slot.sav"));

            Assert.Throws<IOException>(() => storage.Exists("slot"));
            Assert.Throws<IOException>(() => storage.Delete("slot"));
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
