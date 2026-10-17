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
