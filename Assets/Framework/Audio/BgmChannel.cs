using System.Collections;
using Framework.Coroutine;
using UnityEngine;

namespace Framework.Audio
{
    /// <summary>BGM 通道：双 AudioSource 交叉淡入淡出。</summary>
    sealed class BgmChannel
    {
        readonly AudioSource _sourceA;
        readonly AudioSource _sourceB;
        readonly AudioVolumeSettings _volume;

        AudioSource _active;
        AudioSource _idle;
        ICoroutineHandle _fadeHandle;

        /// <summary>当前是否在播放 BGM。</summary>
        public bool IsPlaying => _active != null && _active.isPlaying;

        /// <summary>创建 BGM 通道。</summary>
        /// <param name="root">AudioSource 挂载父节点。</param>
        /// <param name="volume">音量设置。</param>
        public BgmChannel(Transform root, AudioVolumeSettings volume)
        {
            _volume = volume;
            _sourceA = CreateSource(root, "BgmA");
            _sourceB = CreateSource(root, "BgmB");
            _active = _sourceA;
            _idle = _sourceB;
        }

        /// <summary>播放 BGM，可选交叉淡入淡出。</summary>
        /// <param name="clip">音频 Clip；为 null 时不播放。</param>
        /// <param name="fadeSeconds">淡入淡出时长（秒）；小于等于 0 时立即切换。</param>
        public void Play(AudioClip clip, float fadeSeconds)
        {
            StopFade();
            if (clip == null)
            {
                StopImmediate();
                return;
            }

            if (_active != null && _active.isPlaying && _active.clip == clip)
            {
                _active.volume = _volume.GetBgmVolume();
                return;
            }

            if (fadeSeconds <= 0f)
            {
                StopImmediate();
                _active = _idle;
                _idle = _active == _sourceA ? _sourceB : _sourceA;
                _active.clip = clip;
                _active.loop = true;
                _active.volume = _volume.GetBgmVolume();
                _active.Play();
                return;
            }

            _fadeHandle = GameCoroutine.StartGlobal(CrossFadeCoroutine(clip, fadeSeconds));
        }

        /// <summary>停止 BGM。</summary>
        /// <param name="fadeSeconds">淡出时长（秒）；小于等于 0 时立即停止。</param>
        public void Stop(float fadeSeconds)
        {
            StopFade();
            if (_active == null || !_active.isPlaying)
            {
                return;
            }

            if (fadeSeconds <= 0f)
            {
                StopImmediate();
                return;
            }

            _fadeHandle = GameCoroutine.StartGlobal(FadeOutCoroutine(_active, fadeSeconds, StopImmediate));
        }

        /// <summary>刷新当前 BGM 音量。</summary>
        public void RefreshVolume()
        {
            if (_active != null && _active.isPlaying)
            {
                _active.volume = _volume.GetBgmVolume();
            }
        }

        /// <summary>停止全部 AudioSource。</summary>
        public void StopImmediate()
        {
            StopFade();
            _sourceA.Stop();
            _sourceB.Stop();
            _sourceA.clip = null;
            _sourceB.clip = null;
            _active = _sourceA;
            _idle = _sourceB;
        }

        IEnumerator CrossFadeCoroutine(AudioClip clip, float fadeSeconds)
        {
            var fadeOut = _active;
            var fadeIn = _idle;

            fadeIn.clip = clip;
            fadeIn.loop = true;
            fadeIn.volume = 0f;
            fadeIn.Play();

            var elapsed = 0f;
            var startOut = fadeOut != null && fadeOut.isPlaying ? fadeOut.volume : 0f;
            var targetIn = _volume.GetBgmVolume();

            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / fadeSeconds);
                if (fadeOut != null && fadeOut.isPlaying)
                {
                    fadeOut.volume = Mathf.Lerp(startOut, 0f, t);
                }

                fadeIn.volume = Mathf.Lerp(0f, targetIn, t);
                yield return null;
            }

            if (fadeOut != null)
            {
                fadeOut.Stop();
                fadeOut.clip = null;
            }

            fadeIn.volume = targetIn;
            _active = fadeIn;
            _idle = fadeIn == _sourceA ? _sourceB : _sourceA;
            _fadeHandle = null;
        }

        IEnumerator FadeOutCoroutine(AudioSource source, float fadeSeconds, System.Action onComplete)
        {
            var elapsed = 0f;
            var start = source.volume;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                source.volume = Mathf.Lerp(start, 0f, Mathf.Clamp01(elapsed / fadeSeconds));
                yield return null;
            }

            onComplete?.Invoke();
            _fadeHandle = null;
        }

        void StopFade()
        {
            if (_fadeHandle != null)
            {
                GameCoroutine.Stop(_fadeHandle);
                _fadeHandle = null;
            }
        }

        static AudioSource CreateSource(Transform root, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
