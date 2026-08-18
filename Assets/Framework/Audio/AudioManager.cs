using System.Collections;
using Framework.Core;
using Framework.Coroutine;
using Framework.Logging;
using UnityEngine;

namespace Framework.Audio
{
    /// <summary>
    /// 音频管理器：BGM / SFX 双通道、Clip 缓存、音量控制、淡入淡出。
    /// 参考 JSAM / GameFramework 频道模型；Clip 加载统一走 <see cref="Framework.Res.ResourceManager"/>。
    /// </summary>
    public sealed class AudioManager : PersistentSingleton<AudioManager>
    {
        const int DefaultSfxPoolSize = 16;
        const float DefaultBgmFadeSeconds = 0.5f;

        readonly AudioClipCache _clipCache = new AudioClipCache();
        readonly AudioVolumeSettings _volume = new AudioVolumeSettings();

        Transform _audioRoot;
        BgmChannel _bgm;
        SfxChannel _sfx;
        bool _initialized;

        /// <summary>主音量，范围 [0, 1]。</summary>
        public float MasterVolume
        {
            get => _volume.MasterVolume;
            set
            {
                _volume.MasterVolume = Mathf.Clamp01(value);
                RefreshChannelVolumes();
            }
        }

        /// <summary>BGM 音量，范围 [0, 1]。</summary>
        public float BgmVolume
        {
            get => _volume.BgmVolume;
            set
            {
                _volume.BgmVolume = Mathf.Clamp01(value);
                RefreshChannelVolumes();
            }
        }

        /// <summary>SFX 音量，范围 [0, 1]。</summary>
        public float SfxVolume
        {
            get => _volume.SfxVolume;
            set
            {
                _volume.SfxVolume = Mathf.Clamp01(value);
                RefreshChannelVolumes();
            }
        }

        /// <summary>全局静音。</summary>
        public bool Mute
        {
            get => _volume.Mute;
            set
            {
                _volume.Mute = value;
                RefreshChannelVolumes();
            }
        }

        /// <summary>是否已完成初始化。</summary>
        public bool IsInitialized => _initialized;

        /// <summary>初始化音频根节点与 BGM/SFX 通道。</summary>
        /// <param name="sfxPoolSize">SFX AudioSource 池大小；小于等于 0 时使用默认值。</param>
        public void Initialize(int sfxPoolSize = DefaultSfxPoolSize)
        {
            if (_initialized)
            {
                return;
            }

            _volume.Load();

            var rootGo = new GameObject("AudioRoot");
            _audioRoot = rootGo.transform;
            _audioRoot.SetParent(transform, false);

            _bgm = new BgmChannel(_audioRoot, _volume);
            _sfx = new SfxChannel(_audioRoot, _volume, sfxPoolSize > 0 ? sfxPoolSize : DefaultSfxPoolSize);
            _initialized = true;

            GameLog.Info(LogCategories.Audio,
                $"Ready  SfxPool={LogStyle.Value(_sfx.PoolSize)}  Master={LogStyle.Value(_volume.MasterVolume)}");
        }

        /// <summary>关闭全部音频并释放 Clip 缓存。</summary>
        public void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }

            _bgm?.StopImmediate();
            _sfx?.StopAllLoops();
            _clipCache.ReleaseAll();
            _volume.Save();

            if (_audioRoot != null)
            {
                Destroy(_audioRoot.gameObject);
                _audioRoot = null;
            }

            _bgm = null;
            _sfx = null;
            _initialized = false;
        }

        /// <summary>保存音量设置到 PlayerPrefs。</summary>
        public void SaveVolumeSettings()
        {
            _volume.Save();
        }

        /// <summary>同步播放 BGM。</summary>
        /// <param name="location">YooAsset 寻址字符串。</param>
        /// <param name="fadeSeconds">交叉淡入淡出时长（秒）。</param>
        public void PlayBgm(string location, float fadeSeconds = DefaultBgmFadeSeconds)
        {
            EnsureInitialized();
            var clip = _clipCache.GetOrLoadSync(location);
            _bgm.Play(clip, fadeSeconds);
        }

        /// <summary>同步播放 BGM（直接使用 Clip）。</summary>
        /// <param name="clip">音频 Clip。</param>
        /// <param name="fadeSeconds">交叉淡入淡出时长（秒）。</param>
        public void PlayBgm(AudioClip clip, float fadeSeconds = DefaultBgmFadeSeconds)
        {
            EnsureInitialized();
            _bgm.Play(clip, fadeSeconds);
        }

        /// <summary>异步加载并播放 BGM。</summary>
        /// <param name="location">YooAsset 寻址字符串。</param>
        /// <param name="fadeSeconds">交叉淡入淡出时长（秒）。</param>
        /// <returns>协程句柄。</returns>
        public ICoroutineHandle PlayBgmAsync(string location, float fadeSeconds = DefaultBgmFadeSeconds)
        {
            EnsureInitialized();
            return GameCoroutine.StartGlobal(PlayBgmAsyncCoroutine(location, fadeSeconds));
        }

        /// <summary>停止 BGM。</summary>
        /// <param name="fadeSeconds">淡出时长（秒）。</param>
        public void StopBgm(float fadeSeconds = DefaultBgmFadeSeconds)
        {
            if (!_initialized)
            {
                return;
            }

            _bgm.Stop(fadeSeconds);
        }

        /// <summary>BGM 是否正在播放。</summary>
        /// <returns>正在播放返回 true。</returns>
        public bool IsBgmPlaying()
        {
            return _initialized && _bgm.IsPlaying;
        }

        /// <summary>同步播放 2D 音效。</summary>
        /// <param name="location">YooAsset 寻址字符串。</param>
        /// <param name="volumeScale">音量缩放。</param>
        /// <param name="pitch">音高。</param>
        public void PlaySfx(string location, float volumeScale = 1f, float pitch = 1f)
        {
            EnsureInitialized();
            var clip = _clipCache.GetOrLoadSync(location);
            _sfx.PlayOneShot(clip, volumeScale, pitch);
        }

        /// <summary>同步播放 2D 音效（直接使用 Clip）。</summary>
        /// <param name="clip">音频 Clip。</param>
        /// <param name="volumeScale">音量缩放。</param>
        /// <param name="pitch">音高。</param>
        public void PlaySfx(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
        {
            EnsureInitialized();
            _sfx.PlayOneShot(clip, volumeScale, pitch);
        }

        /// <summary>同步播放 3D 音效。</summary>
        /// <param name="location">YooAsset 寻址字符串。</param>
        /// <param name="worldPosition">世界坐标。</param>
        /// <param name="volumeScale">音量缩放。</param>
        /// <param name="pitch">音高。</param>
        public void PlaySfxAt(string location, Vector3 worldPosition, float volumeScale = 1f, float pitch = 1f)
        {
            EnsureInitialized();
            var clip = _clipCache.GetOrLoadSync(location);
            _sfx.PlayOneShotAt(clip, worldPosition, volumeScale, pitch);
        }

        /// <summary>播放循环音效。</summary>
        /// <param name="location">YooAsset 寻址字符串。</param>
        /// <param name="volumeScale">音量缩放。</param>
        /// <returns>循环音效 id；失败返回 0。</returns>
        public int PlaySfxLoop(string location, float volumeScale = 1f)
        {
            EnsureInitialized();
            var clip = _clipCache.GetOrLoadSync(location);
            return _sfx.PlayLoop(clip, volumeScale);
        }

        /// <summary>停止指定循环音效。</summary>
        /// <param name="loopId">由 <see cref="PlaySfxLoop"/> 返回的 id。</param>
        public void StopSfxLoop(int loopId)
        {
            if (!_initialized)
            {
                return;
            }

            _sfx.StopLoop(loopId);
        }

        /// <summary>停止全部循环音效。</summary>
        public void StopAllSfxLoops()
        {
            if (!_initialized)
            {
                return;
            }

            _sfx.StopAllLoops();
        }

        IEnumerator PlayBgmAsyncCoroutine(string location, float fadeSeconds)
        {
            AudioClip clip = null;
            yield return _clipCache.LoadAsync(location, c => clip = c);
            _bgm.Play(clip, fadeSeconds);
        }

        void RefreshChannelVolumes()
        {
            if (!_initialized)
            {
                return;
            }

            _bgm.RefreshVolume();
            _sfx.RefreshVolume();
        }

        void EnsureInitialized()
        {
            if (!_initialized)
            {
                throw new System.InvalidOperationException("[Audio] AudioManager is not initialized.");
            }
        }
    }
}
