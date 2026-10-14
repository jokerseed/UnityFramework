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
