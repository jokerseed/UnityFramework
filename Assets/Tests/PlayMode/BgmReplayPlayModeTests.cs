using System;
using System.Collections;
using System.Reflection;
using Framework.Audio;
using Framework.Coroutine;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Framework.Tests
{
    public sealed class BgmReplayPlayModeTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        const float FadeSeconds = 0.2f;
        const int SampleRate = 44100;
        GameObject _host;
        CoroutineManager _manager;
        AudioClip _first;
        AudioClip _second;
        AudioVolumeSettings _volume;
        Type _channelType;
        object _channel;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("BgmReplayPlayModeTests");
            _manager = _host.AddComponent<CoroutineManager>();
            _first = AudioClip.Create("first", SampleRate * 30, 1, SampleRate, false);
            _second = AudioClip.Create("second", SampleRate * 30, 1, SampleRate, false);
            _volume = new AudioVolumeSettings { MasterVolume = 0.8f, BgmVolume = 0.5f };
            _channelType = typeof(AudioManager).Assembly.GetType("Framework.Audio.BgmChannel", true);
            _channel = Activator.CreateInstance(_channelType, _host.transform, _volume);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_channel != null) _channelType.GetMethod("StopImmediate").Invoke(_channel, null);
            if (_manager != null) _manager.Shutdown();
            if (_host != null) Object.Destroy(_host);
            if (_first != null) Object.Destroy(_first);
            if (_second != null) Object.Destroy(_second);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReplayActiveClipDuringStopFadeResumesWithoutRewinding()
        {
            yield return ReplayDuringStopFade(FadeSeconds, false, true);
        }

        [UnityTest]
        public IEnumerator ImmediateReplayDuringStopFadeDoesNotRewind()
        {
            yield return ReplayDuringStopFade(0f, false);
        }

        [UnityTest]
        public IEnumerator ReplayDuringStopFadeKeepsOtherSourceFadingOut()
        {
            yield return ReplayDuringStopFade(1f, true, true);
        }

        [UnityTest]
        public IEnumerator ImmediateReplayDuringStopFadeStopsOnlyOtherSource()
        {
            yield return ReplayDuringStopFade(0f, true);
        }

        [UnityTest]
        public IEnumerator DuplicatePlayDuringCrossfadeKeepsExistingFadeAndPosition()
        {
            Play(_first, 0f);
            Play(_second, FadeSeconds);
            var active = Active;
            active.timeSamples = SampleRate * 3;
            var position = active.timeSamples;
            Assert.That(position, Is.GreaterThan(SampleRate), "The test must seek away from the clip start.");
            var fade = Fade;
            Assert.That(fade.IsRunning, Is.True);

            Play(_second, FadeSeconds);

            Assert.That(Fade, Is.SameAs(fade), "Duplicate play must not restart an ordinary crossfade.");
            Assert.That(Active, Is.SameAs(active));
            Assert.That(active.timeSamples, Is.GreaterThanOrEqualTo(position));
            yield return WaitForFade(fade);
            Assert.That(active.isPlaying, Is.True);
            Assert.That(active.volume, Is.EqualTo(_volume.GetBgmVolume()).Within(0.0001f));
        }

        IEnumerator ReplayDuringStopFade(float replayFadeSeconds, bool crossfadeFirst, bool waitBeforeReplay = false)
        {
            Play(_first, 0f);
            if (crossfadeFirst)
            {
                Play(_second, waitBeforeReplay ? 1f : FadeSeconds);
                if (waitBeforeReplay) yield return new WaitForSecondsRealtime(0.05f);
            }
            var active = Active;
            var other = Source("_sourceA") == active ? Source("_sourceB") : Source("_sourceA");
            var clip = active.clip;
            Assert.That(active.isPlaying, Is.True);
            Assert.That(other.isPlaying, Is.EqualTo(crossfadeFirst));
            active.timeSamples = SampleRate * 3;

            var stopFadeSeconds = waitBeforeReplay ? 1f : FadeSeconds;
            var beforeStopVolume = active.volume;
            Stop(stopFadeSeconds);
            var oldStop = Fade;
            if (waitBeforeReplay)
            {
                // Also exercise reversal after actual frames of attenuation, not only same-frame requests.
                yield return new WaitForSecondsRealtime(0.05f);
                Assert.That(active.volume, Is.LessThan(beforeStopVolume));
                Assert.That(active.volume, Is.GreaterThan(0f));
            }
            Assert.That(oldStop.IsRunning, Is.True);
            var position = active.timeSamples;
            Assert.That(position, Is.GreaterThan(SampleRate), "The test must seek away from the clip start.");
            var startVolume = active.volume;
            Play(clip, replayFadeSeconds);

            Assert.That(oldStop.IsRunning, Is.False, "The new play must cancel the old stop fade.");
            Assert.That(Active, Is.SameAs(active));
            Assert.That(active.clip, Is.SameAs(clip));
            Assert.That(active.isPlaying, Is.True);
            Assert.That(active.timeSamples, Is.GreaterThanOrEqualTo(position), "Replay must not rewind the clip.");
            Assert.That(active.volume, Is.GreaterThanOrEqualTo(startVolume));
            if (replayFadeSeconds > 0f)
            {
                Assert.That(other.isPlaying, Is.EqualTo(crossfadeFirst), "The other source should continue fading out.");
                if (crossfadeFirst && waitBeforeReplay)
                {
                    var otherVolume = other.volume;
                    // Two frames avoid depending on test/fade coroutine resume order within one frame.
                    yield return null;
                    yield return null;
                    Assert.That(other.isPlaying, Is.True, "Reversal must fade the other source, not stop it immediately.");
                    Assert.That(other.volume, Is.LessThan(otherVolume));
                }
                // Fade targets remain live, including after reversing a stop.
                _volume.BgmVolume = 0.75f;
                yield return WaitForFade(Fade);
            }
            else
            {
                Assert.That(Fade, Is.Null);
                Assert.That(other.isPlaying, Is.False);
            }

            // Advance past the old stop deadline as well as the new fade completion.
            yield return new WaitForSecondsRealtime(stopFadeSeconds + 0.05f);
            Assert.That(active.isPlaying, Is.True, "A stale stop must not clear a newer playback request.");
            Assert.That(active.clip, Is.SameAs(clip));
            Assert.That(active.volume, Is.EqualTo(_volume.GetBgmVolume()).Within(0.0001f));
            Assert.That(other.isPlaying, Is.False);
            Assert.That(other.clip, Is.Null);

            // Clearing the stop state must not prevent a later legitimate stop.
            Stop(FadeSeconds);
            yield return WaitForFade(Fade);
            Assert.That(active.isPlaying, Is.False);
            Assert.That(active.clip, Is.Null);
        }

        static IEnumerator WaitForFade(ICoroutineHandle fade)
        {
            Assert.That(fade, Is.Not.Null);
            var deadline = Time.realtimeSinceStartup + 5f;
            while (fade.IsRunning && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(fade.IsRunning, Is.False, "BGM fade did not finish within five seconds.");
        }

        AudioSource Active => Source("_active");
        ICoroutineHandle Fade => (ICoroutineHandle)_channelType.GetField("_fadeHandle", Private).GetValue(_channel);
        AudioSource Source(string field) => (AudioSource)_channelType.GetField(field, Private).GetValue(_channel);
        void Play(AudioClip clip, float seconds) => _channelType.GetMethod("Play").Invoke(_channel, new object[] { clip, seconds });
        void Stop(float seconds) => _channelType.GetMethod("Stop").Invoke(_channel, new object[] { seconds });
    }
}
