using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Framework.Audio;
using Framework.Res;
using Framework.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Framework.Tests
{
    public sealed class AudioUiLifecycleTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        GameObject _host;
        AudioClip _clip;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("AudioUiLifecycleTests");
            _clip = AudioClip.Create("test", 44100, 1, 44100, false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_clip);
        }

        [Test]
        public void OneShotReusedAfterLoopHasSingleGainAndRefreshesWhenMuted()
        {
            var type = typeof(AudioManager).Assembly.GetType("Framework.Audio.SfxChannel", true);
            var volumeType = typeof(AudioManager).Assembly.GetType("Framework.Audio.AudioVolumeSettings", true);
            var volume = Activator.CreateInstance(volumeType, true);
            volumeType.GetProperty("MasterVolume").SetValue(volume, 0.5f);
            volumeType.GetProperty("SfxVolume").SetValue(volume, 0.5f);
            var channel = Activator.CreateInstance(type, _host.transform, volume, 1);
            var loop = type.GetMethod("PlayLoop").Invoke(channel, new object[] { _clip, 0.2f });
            type.GetMethod("StopLoop").Invoke(channel, new[] { loop });
            type.GetMethod("PlayOneShot").Invoke(channel, new object[] { _clip, 0.4f, 1f });
            var source = _host.GetComponentInChildren<AudioSource>();
            Assert.That(source.volume, Is.EqualTo(0.1f).Within(0.0001f));
            volumeType.GetProperty("Mute").SetValue(volume, true);
            type.GetMethod("RefreshVolume").Invoke(channel, null);
            Assert.That(source.volume, Is.Zero);
            volumeType.GetProperty("Mute").SetValue(volume, false);
            type.GetMethod("RefreshVolume").Invoke(channel, null);
            Assert.That(source.volume, Is.EqualTo(0.1f).Within(0.0001f));
        }

        [Test]
        public void CacheTrimProtectsAcquiringEntryEvenWhenItIsOldest()
        {
            var type = typeof(AudioManager).Assembly.GetType("Framework.Audio.AudioClipCache", true);
            var cache = Activator.CreateInstance(type, true);
            type.GetProperty("Capacity").SetValue(cache, 1);
            var handles = (Dictionary<string, ResourceAssetHandle>)type.GetField("_handles", Private).GetValue(cache);
            var uses = (Dictionary<string, long>)type.GetField("_lastUse", Private).GetValue(cache);
            handles.Add("acquiring", default);
            handles.Add("other", default);
            uses.Add("acquiring", 1);
            uses.Add("other", 2);
            type.GetMethod("Trim", Private).Invoke(cache, new object[] { "acquiring" });
            Assert.That(handles.ContainsKey("acquiring"), Is.True);
            Assert.That(handles.Count, Is.EqualTo(1));
        }

        [Test]
        public void OldCacheWaiterDoesNotJoinNewGenerationLoad()
        {
            var type = typeof(AudioManager).Assembly.GetType("Framework.Audio.AudioClipCache", true);
            var cache = Activator.CreateInstance(type, true);
            var loading = (HashSet<string>)type.GetField("_loading", Private).GetValue(cache);
            loading.Add("song");
            var completed = false;
            Action<AudioClip> callback = clip => { completed = true; Assert.That(clip, Is.Null); };
            var waiter = (IEnumerator)type.GetMethod("LoadAsync").Invoke(cache, new object[] { "song", callback });
            Assert.That(waiter.MoveNext(), Is.True);
            type.GetMethod("ReleaseAll").Invoke(cache, null);
            loading.Add("song");
            Assert.That(waiter.MoveNext(), Is.False);
            Assert.That(completed, Is.True);
            Assert.That(loading.Contains("song"), Is.True);
        }

        [Test]
        public void StaleBgmCompletionCannotTouchNewSessionChannel()
        {
            var manager = _host.AddComponent<AudioManager>();
            // A stale completion must be ignored even when a newer session is initialized.
            typeof(AudioManager).GetField("_initialized", Private).SetValue(manager, true);
            typeof(AudioManager).GetField("_bgmRequestVersion", Private).SetValue(manager, 2L);
            var operation = (IEnumerator)typeof(AudioManager).GetMethod("PlayBgmAsyncCoroutine", Private)
                .Invoke(manager, new object[] { "", 0f, 1L });
            Assert.That(operation.MoveNext(), Is.True);
            Assert.That(((IEnumerator)operation.Current).MoveNext(), Is.False);
            Assert.That(operation.MoveNext(), Is.False);
            typeof(AudioManager).GetField("_initialized", Private).SetValue(manager, false);
        }

        [Test]
        public void LayerCapacityRejectsSpillWithoutChangingExistingOrders()
        {
            var manager = _host.AddComponent<UIManager>();
            var bringToTop = typeof(UIManager).GetMethod("BringToTop", Private);
            var stack = (IReadOnlyList<UIWindow>)typeof(UIManager).GetProperty("Stack", Private).GetValue(manager);
            for (var i = 0; i < 500; i++) bringToTop.Invoke(manager, new object[] { new TestWindow() });
            Assert.That(stack[499].SortingOrder, Is.EqualTo(4990));
            var error = Assert.Throws<TargetInvocationException>(() => bringToTop.Invoke(manager, new object[] { new TestWindow() }));
            Assert.That(error.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(stack.Count, Is.EqualTo(500));
        }

        [Test]
        public void DestroyCallbackFailureStillClearsUiStateAndPreventsReentry()
        {
            var window = new ThrowingWindow();
            typeof(UIBase).GetMethod("InternalCreate", Private).Invoke(window, new object[] { null, _host, null, null });
            LogAssert.ignoreFailingMessages = true;
            try
            {
                typeof(UIBase).GetMethod("InternalDestroy", Private).Invoke(window, null);
                typeof(UIBase).GetMethod("InternalDestroy", Private).Invoke(window, null);
                Assert.That(window.IsCreated, Is.False);
                Assert.That(window.GameObject, Is.Null);
                Assert.That(window.DestroyCalls, Is.EqualTo(1));
            }
            finally { LogAssert.ignoreFailingMessages = false; }
        }

        sealed class TestWindow : UIWindow { }
        sealed class ThrowingWindow : UIWindow
        {
            public int DestroyCalls;
            protected override void OnDestroy()
            {
                DestroyCalls++;
                throw new InvalidOperationException("test callback");
            }
        }
    }
}
