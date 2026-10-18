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

        [Test]
        public void StripsRichTextFromMessage()
        {
            using (var sink = new FileLogSink(_dir))
            {
                var message = $"Group {"<b><color=#80DEEA>launch</color></b>"} ready";
                var entry = new LogEntry(LogLevel.Info, "Test", message, null, DateTime.UtcNow);
                sink.Write(in entry);
                sink.Flush();
                var text = ReadShared(sink.CurrentPath);
                StringAssert.Contains("Group launch ready", text);
                StringAssert.DoesNotContain("<color", text);
                StringAssert.DoesNotContain("<b>", text);
            }
        }

        [Test]
        public void InfoIsBufferedUntilFlush()
        {
            using (var sink = new FileLogSink(_dir))
            {
                var entry = new LogEntry(LogLevel.Info, "Test", "buffered", null, DateTime.UtcNow);
                sink.Write(in entry);
                sink.Flush();
                StringAssert.Contains("buffered", ReadShared(sink.CurrentPath));
            }
        }

        [Test]
        public void WriteAfterDisposeDoesNotThrow()
        {
            var sink = new FileLogSink(_dir);
            sink.Dispose();
            var entry = new LogEntry(LogLevel.Error, "Test", "late", null, DateTime.UtcNow);
            Assert.DoesNotThrow(() => sink.Write(in entry));
        }

        [Test]
        public void GameLogSurvivesThrowingSink()
        {
            GameLog.Shutdown();
            try
            {
                GameLog.AddSink(new ThrowingSink());
                Assert.DoesNotThrow(() => GameLog.Error("Test", "should not throw"));
            }
            finally
            {
                GameLog.Shutdown();
            }
        }

        sealed class ThrowingSink : ILogSink
        {
            public void Write(in LogEntry entry) => throw new InvalidOperationException("sink failed");
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
