using System.Collections;
using Framework.Coroutine;
using UnityEngine;

namespace Framework.Audio
{
    /// <summary>
    /// BGM 通道：A/B 两个 AudioSource 交叉淡入淡出。
    /// 淡入淡出途中再次切歌或停止时，以两个音源当前的实际音量为起点继续：
    /// 两个都在响时较轻的那个立即停掉、较响的继续淡出，新曲用空出的音源淡入；
    /// 请求的正是正在淡入（或正在淡出）的那首时不会从头重播。淡入淡出期间每帧读取最新的目标音量。
    /// </summary>
    sealed class BgmChannel
    {
        readonly AudioSource _sourceA;
        readonly AudioSource _sourceB;
        readonly AudioVolumeSettings _volume;

        AudioSource _active;
        ICoroutineHandle _fadeHandle;

        public bool IsPlaying => _active != null && _active.isPlaying;

        public BgmChannel(Transform root, AudioVolumeSettings volume)
        {
            _volume = volume;
            _sourceA = CreateSource(root, "BgmA");
            _sourceB = CreateSource(root, "BgmB");
            _active = _sourceA;
        }

        /// <summary>指定 Clip 是否正在任一 BGM 音源上播放（含淡出中）。</summary>
        /// <param name="clip">音频 Clip。</param>
        /// <returns>正在播放返回 true。</returns>
        public bool IsUsing(AudioClip clip)
        {
            return clip != null
                   && ((_sourceA.isPlaying && _sourceA.clip == clip) || (_sourceB.isPlaying && _sourceB.clip == clip));
        }

        public void Play(AudioClip clip, float fadeSeconds)
        {
            if (clip == null)
            {
                StopImmediate();
                return;
            }

            if (_active.isPlaying && _active.clip == clip)
            {
                // 已是当前曲目（可能正在淡入）：淡入照常进行；没有淡入时把音量对齐到目标值。
                if (!IsFading)
                {
                    _active.volume = _volume.GetBgmVolume();
                }

                return;
            }

            StopFade();
            var other = Other(_active);
            if (other.isPlaying && other.clip == clip)
            {
                // 切回正在淡出的那首：反向淡入它，不从头播放。
                StartFade(other, _active, fadeSeconds);
                return;
            }

            // 两个都在响时保留较响的一个继续淡出，较轻的直接停掉腾出音源。
            AudioSource fadingOut = null;
            if (_sourceA.isPlaying && _sourceB.isPlaying)
            {
                fadingOut = _sourceA.volume >= _sourceB.volume ? _sourceA : _sourceB;
                StopSource(Other(fadingOut));
            }
            else if (_sourceA.isPlaying)
            {
                fadingOut = _sourceA;
            }
            else if (_sourceB.isPlaying)
            {
                fadingOut = _sourceB;
            }

            var incoming = fadingOut != null ? Other(fadingOut) : _active;
            incoming.clip = clip;
            incoming.loop = true;
            incoming.volume = fadeSeconds > 0f ? 0f : _volume.GetBgmVolume();
            incoming.Play();
            StartFade(incoming, fadingOut, fadeSeconds);
        }

        public void Stop(float fadeSeconds)
        {
            StopFade();
            if (!_sourceA.isPlaying && !_sourceB.isPlaying)
            {
                return;
            }

            if (fadeSeconds <= 0f)
            {
                StopImmediate();
                return;
            }

            _fadeHandle = GameCoroutine.StartGlobal(FadeOutAllCoroutine(fadeSeconds));
        }

        public void RefreshVolume()
        {
            // 淡入淡出中每帧都会读取最新的目标音量，这里只处理静止状态。
            if (!IsFading && _active != null && _active.isPlaying)
            {
                _active.volume = _volume.GetBgmVolume();
            }
        }

        public void StopImmediate()
        {
            StopFade();
            StopSource(_sourceA);
            StopSource(_sourceB);
            _active = _sourceA;
        }

        bool IsFading => _fadeHandle != null && _fadeHandle.IsRunning;

        AudioSource Other(AudioSource source) => source == _sourceA ? _sourceB : _sourceA;

        void StartFade(AudioSource fadeIn, AudioSource fadeOut, float fadeSeconds)
        {
            _active = fadeIn;
            if (fadeSeconds <= 0f)
            {
                if (fadeOut != null)
                {
                    StopSource(fadeOut);
                }

                fadeIn.volume = _volume.GetBgmVolume();
                return;
            }

            _fadeHandle = GameCoroutine.StartGlobal(CrossFadeCoroutine(fadeIn, fadeOut, fadeSeconds));
        }

        IEnumerator CrossFadeCoroutine(AudioSource fadeIn, AudioSource fadeOut, float fadeSeconds)
        {
            var elapsed = 0f;
            var startIn = fadeIn.volume;
            var startOut = fadeOut != null && fadeOut.isPlaying ? fadeOut.volume : 0f;

            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / fadeSeconds);
                fadeIn.volume = Mathf.Lerp(startIn, _volume.GetBgmVolume(), t);
                if (fadeOut != null && fadeOut.isPlaying)
                {
                    fadeOut.volume = Mathf.Lerp(startOut, 0f, t);
                }

                yield return null;
            }

            if (fadeOut != null)
            {
                StopSource(fadeOut);
            }

            fadeIn.volume = _volume.GetBgmVolume();
            _fadeHandle = null;
        }

        IEnumerator FadeOutAllCoroutine(float fadeSeconds)
        {
            var elapsed = 0f;
            var startA = _sourceA.volume;
            var startB = _sourceB.volume;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / fadeSeconds);
                _sourceA.volume = Mathf.Lerp(startA, 0f, t);
                _sourceB.volume = Mathf.Lerp(startB, 0f, t);
                yield return null;
            }

            _fadeHandle = null;
            StopImmediate();
        }

        void StopFade()
        {
            if (_fadeHandle != null)
            {
                GameCoroutine.Stop(_fadeHandle);
                _fadeHandle = null;
            }
        }

        static void StopSource(AudioSource source)
        {
            source.Stop();
            source.clip = null;
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
