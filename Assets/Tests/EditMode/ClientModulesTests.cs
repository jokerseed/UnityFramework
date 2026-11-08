using System;
using System.IO;
using Framework.Localization;
using Framework.Save;
using Framework.Timer;
using NUnit.Framework;
using UnityEngine;

namespace Framework.Tests
{
    /// <summary>定时器、存档、本地化模块的行为测试。</summary>
    public sealed class ClientModulesTests
    {
        GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("client-modules-test");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_host);
        }

        [Test]
        public void TimerStartsNextTickAndHonoursRepeatCount()
        {
            var timers = _host.AddComponent<TimerManager>();
            var frame = Time.frameCount;
            var fired = 0;
            var handle = timers.Repeat(0.5f, () => fired++, count: 2);

            timers.Tick(10f, 10f, frame);
            Assert.AreEqual(0, fired, "新定时器当帧不计时。");

            timers.Tick(0.5f, 0.5f, ++frame);
            Assert.AreEqual(1, fired);
            timers.Tick(0.5f, 0.5f, ++frame);
            Assert.AreEqual(2, fired);
            Assert.IsFalse(timers.IsActive(handle));
            timers.Tick(0.5f, 0.5f, ++frame);
            Assert.AreEqual(2, fired);
        }

        [Test]
        public void TimerCancelAndOwnerLifetime()
        {
            var timers = _host.AddComponent<TimerManager>();
            var frame = Time.frameCount;
            var fired = 0;
            var cancelled = timers.Delay(0.1f, () => fired++);
            var owner = new GameObject("timer-owner");
            timers.Delay(0.1f, () => fired++, owner);
            Assert.IsTrue(timers.Cancel(cancelled));
            Assert.IsFalse(timers.Cancel(cancelled), "失效句柄不能再取消。");

            UnityEngine.Object.DestroyImmediate(owner);
            timers.Tick(0f, 0f, ++frame);
            timers.Tick(1f, 1f, ++frame);
            Assert.AreEqual(0, fired);
            Assert.AreEqual(0, timers.ActiveCount);
        }

        [Test]
        public void TimerUsesUnscaledTimeWhenRequested()
        {
            var timers = _host.AddComponent<TimerManager>();
            var frame = Time.frameCount;
            var fired = false;
            timers.Delay(1f, () => fired = true, unscaled: true);
            timers.Tick(0f, 0f, ++frame);
            timers.Tick(0f, 1f, ++frame);
            Assert.IsTrue(fired);
        }

        [Serializable]
        sealed class Profile
        {
            public string Name;
            public int Level;
        }

        [Test]
        public void SaveRoundTripListAndOverwrite()
        {
            var dir = Path.Combine(Path.GetTempPath(), "framework-save-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var saves = _host.AddComponent<SaveManager>();
                saves.Configure(new FileSaveStorage(dir), new JsonUtilitySaveSerializer());
                saves.Save("profile", new Profile { Name = "a", Level = 1 });
                saves.Save("profile", new Profile { Name = "b", Level = 2 });
                saves.Save("slot.2", new Profile { Name = "c", Level = 3 });

                Assert.IsTrue(saves.TryLoad("profile", out Profile loaded));
                Assert.AreEqual("b", loaded.Name);
                Assert.AreEqual(2, loaded.Level);
                CollectionAssert.AreEqual(new[] { "profile", "slot.2" }, saves.ListKeys());
                Assert.IsEmpty(Directory.GetFiles(dir, "*.tmp"), "覆盖写入不应残留临时文件。");

                Assert.IsTrue(saves.Delete("slot.2"));
                Assert.IsFalse(saves.TryLoad("slot.2", out Profile _));
                Assert.Throws<ArgumentException>(() => saves.Save("../escape", loaded));

                File.WriteAllText(Path.Combine(dir, "profile.sav"), "{not json");
                var savedEvents = 0;
                saves.Saved += _ => savedEvents++;
                Assert.IsTrue(saves.TryLoad("profile", out loaded));
                Assert.AreEqual("a", loaded.Name);
                Assert.AreEqual(1, loaded.Level);
                Assert.AreEqual(0, savedEvents, "恢复不应被当成用户保存事件。");
                Assert.AreEqual(File.ReadAllText(Path.Combine(dir, "profile.sav.bak")),
                    File.ReadAllText(Path.Combine(dir, "profile.sav")));

                saves.Save("profile", new Profile { Name = "d", Level = 4 });
                File.WriteAllText(Path.Combine(dir, "profile.sav"), "{not json");
                Assert.IsTrue(saves.TryLoad("profile", out loaded));
                Assert.AreEqual("a", loaded.Name, "后续保存不能让坏主档覆盖可用备份。");

                File.WriteAllText(Path.Combine(dir, "profile.sav"), "{not json");
                File.WriteAllText(Path.Combine(dir, "profile.sav.bak"), "{not json");
                Assert.Throws<FormatException>(() => saves.TryLoad("profile", out Profile _));
            }
            finally
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
        }

        [Test]
        public void SaveMissingPrimaryCorruptBackup_DoesNotCreateDefault()
        {
            var dir = Path.Combine(Path.GetTempPath(), "framework-save-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "profile.sav.bak"), "{not json");
                var saves = _host.AddComponent<SaveManager>();
                saves.Configure(new FileSaveStorage(dir), new JsonUtilitySaveSerializer());
                var defaults = 0;

                Assert.Throws<FormatException>(() => saves.LoadOrDefault("profile", () =>
                {
                    defaults++;
                    return new Profile();
                }));
                Assert.AreEqual(0, defaults);
                Assert.IsFalse(File.Exists(Path.Combine(dir, "profile.sav")));
                Assert.AreEqual("{not json", File.ReadAllText(Path.Combine(dir, "profile.sav.bak")));

                Assert.IsNotNull(saves.LoadOrDefault("new-profile", () =>
                {
                    defaults++;
                    return new Profile();
                }));
                Assert.AreEqual(1, defaults);
                Assert.IsFalse(File.Exists(Path.Combine(dir, "new-profile.sav")));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [TestCase(false, false, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, false)]
        [TestCase(true, true, true)]
        [TestCase(false, true, false)]
        [TestCase(false, true, true)]
        public void SaveSlotDiscoveryAndDeletedEvent_IncludeBackupOnly(bool primary, bool backup, bool corruptBackup)
        {
            var dir = Path.Combine(Path.GetTempPath(), "framework-save-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var serializer = new JsonUtilitySaveSerializer();
                var bytes = serializer.Serialize(new Profile { Name = "a", Level = 1 });
                if (primary || backup) Directory.CreateDirectory(dir);
                if (primary) File.WriteAllBytes(Path.Combine(dir, "profile.sav"), bytes);
                if (backup)
                {
                    if (corruptBackup) File.WriteAllText(Path.Combine(dir, "profile.sav.bak"), "{not json");
                    else File.WriteAllBytes(Path.Combine(dir, "profile.sav.bak"), bytes);
                }
                var saves = _host.AddComponent<SaveManager>();
                saves.Configure(new FileSaveStorage(dir), serializer);
                var deletedEvents = 0;
                saves.Deleted += key =>
                {
                    Assert.AreEqual("profile", key);
                    deletedEvents++;
                };

                Assert.AreEqual(primary || backup, saves.Exists("profile"));
                CollectionAssert.AreEqual(primary || backup ? new[] { "profile" } : Array.Empty<string>(), saves.ListKeys());
                Assert.AreEqual(primary, File.Exists(Path.Combine(dir, "profile.sav")), "发现接口不能隐式恢复。");
                if (corruptBackup)
                    Assert.Throws<FormatException>(() => saves.LoadOrDefault("profile", () => new Profile()));

                Assert.AreEqual(primary || backup, saves.Delete("profile"));
                Assert.AreEqual(primary || backup ? 1 : 0, deletedEvents);
                Assert.IsFalse(saves.Delete("profile"));
                Assert.AreEqual(primary || backup ? 1 : 0, deletedEvents, "重复删除不能再次通知。");
                Assert.IsFalse(saves.Exists("profile"));
                Assert.IsEmpty(saves.ListKeys());
                Assert.IsFalse(saves.TryLoad("profile", out Profile _));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void SaveDeleteFailure_DoesNotRaiseDeleted()
        {
            var dir = Path.Combine(Path.GetTempPath(), "framework-save-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                var saves = _host.AddComponent<SaveManager>();
                saves.Configure(new FileSaveStorage(dir), new JsonUtilitySaveSerializer());
                saves.Save("profile", new Profile { Name = "a", Level = 1 });
                Directory.CreateDirectory(Path.Combine(dir, "profile.sav.bak"));
                var deletedEvents = 0;
                saves.Deleted += _ => deletedEvents++;

                Assert.Throws<IOException>(() => saves.Delete("profile"));
                Assert.AreEqual(0, deletedEvents);
                Assert.IsTrue(File.Exists(Path.Combine(dir, "profile.sav")));
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void LocalizationPrefersLaterSourcesAndFallsBack()
        {
            var loc = _host.AddComponent<LocalizationManager>();
            loc.AddSource(new DictionaryLocalizationSource()
                .Set("en", "hello", "Hello")
                .Set("en", "only.en", "English only")
                .Set("zh-Hans", "hello", "你好"));
            loc.AddSource(new DictionaryLocalizationSource().Set("zh-Hans", "hello", "您好"));

            string changedTo = null;
            loc.LanguageChanged += lang => changedTo = lang;
            loc.SetLanguage("zh-Hans");

            Assert.AreEqual("zh-Hans", changedTo);
            Assert.AreEqual("您好", loc.Get("hello"));
            Assert.AreEqual("English only", loc.Get("only.en"));
            Assert.AreEqual("missing.key", loc.Get("missing.key"));

            loc.AddSource(new DictionaryLocalizationSource().Set("en", "lv", "Lv {0}"));
            Assert.AreEqual("Lv 3", loc.Format("lv", 3));
        }
    }
}
