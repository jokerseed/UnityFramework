using System;
using System.IO;
using Framework.Logging;
using NUnit.Framework;

namespace Framework.Sim.Tests
{
    public sealed class FileLogSinkTests
    {
        string _dir;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "fw-log-" + Guid.NewGuid().ToString("N"));
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
        public void RollsAndKeepsAtMostMaxFiles()
        {
            using (var sink = new FileLogSink(_dir, "t", 200, 3))
            {
                for (var i = 0; i < 50; i++)
                {
                    var entry = new LogEntry(LogLevel.Info, "Test", "line " + i, null, DateTime.UtcNow);
                    sink.Write(in entry);
                }
            }

            Assert.IsTrue(File.Exists(Path.Combine(_dir, "t.log")));
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "t.1.log")));
            Assert.IsTrue(File.Exists(Path.Combine(_dir, "t.2.log")));
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "t.3.log")));
            StringAssert.Contains("line 49", File.ReadAllText(Path.Combine(_dir, "t.log")));
        }

        [Test]
        public void WritesExceptionText()
        {
            using (var sink = new FileLogSink(_dir))
            {
                var entry = new LogEntry(LogLevel.Exception, "Test", "boom", new InvalidOperationException("inner"), DateTime.UtcNow);
                sink.Write(in entry);
                StringAssert.Contains("inner", ReadShared(sink.CurrentPath));
            }
        }

        static string ReadShared(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
