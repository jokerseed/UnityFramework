using System.Collections.Generic;
using Framework.Logging;
using UnityEngine;

namespace Framework.Audio
{
    /// <summary>
    /// SFX 通道：AudioSource 对象池，支持 OneShot 与循环音效。
    /// 循环音占用的音源被标记为保留：一次性音效不会抢它，也不会改动它的 pitch、位置或 loop。
    /// 池满时一次性音效抢最早开始的那个一次性音源（先停掉再播）；循环音找不到可用音源时返回 0 并告警。
    /// </summary>
    sealed class SfxChannel
    {
        sealed class Loop
        {
            public AudioSource Source;
            public float VolumeScale;
        }

        readonly List<AudioSource> _pool = new List<AudioSource>(16);
        readonly float[] _startedAt;
        readonly AudioClip[] _oneShotClips;
        readonly bool[] _reserved;
        readonly Dictionary<int, Loop> _loops = new Dictionary<int, Loop>(8);
        readonly AudioVolumeSettings _volume;

        int _nextLoopId = 1;
        int _roundRobin;

        /// <summary>对象池容量。</summary>
        public int PoolSize { get; }

        /// <summary>创建 SFX 通道。</summary>
        /// <param name="root">AudioSource 挂载父节点。</param>
        /// <param name="volume">音量设置。</param>
        /// <param name="poolSize">AudioSource 池大小。</param>
        public SfxChannel(Transform root, AudioVolumeSettings volume, int poolSize)
        {
            _volume = volume;
            PoolSize = Mathf.Max(1, poolSize);
            _startedAt = new float[PoolSize];
            _oneShotClips = new AudioClip[PoolSize];
            _reserved = new bool[PoolSize];

            for (var i = 0; i < PoolSize; i++)
            {
                _pool.Add(CreateSource(root, $"Sfx_{i}"));
            }
        }

        /// <summary>播放 2D 音效。</summary>
        /// <param name="clip">音频 Clip。</param>
        /// <param name="volumeScale">音量缩放。</param>
        /// <param name="pitch">音高。</param>
        public void PlayOneShot(AudioClip clip, float volumeScale = 1f, float pitch = 1f)
        {
            if (clip == null)
            {
                return;
            }

            var index = RentOneShotSource();
            if (index < 0)
            {
                return;
            }

            var source = _pool[index];
            source.transform.localPosition = Vector3.zero;
            source.spatialBlend = 0f;
            source.pitch = pitch;
            source.loop = false;
            MarkOneShot(index, clip);
            source.PlayOneShot(clip, _volume.GetSfxVolume(volumeScale));
        }

        /// <summary>播放 3D 音效。</summary>
        /// <param name="clip">音频 Clip。</param>
        /// <param name="worldPosition">世界坐标。</param>
        /// <param name="volumeScale">音量缩放。</param>
        /// <param name="pitch">音高。</param>
        public void PlayOneShotAt(AudioClip clip, Vector3 worldPosition, float volumeScale = 1f, float pitch = 1f)
        {
            if (clip == null)
            {
                return;
            }

            var index = RentOneShotSource();
            if (index < 0)
            {
                return;
            }

            var source = _pool[index];
            source.transform.position = worldPosition;
            source.spatialBlend = 1f;
            source.pitch = pitch;
            source.loop = false;
            MarkOneShot(index, clip);
            source.PlayOneShot(clip, _volume.GetSfxVolume(volumeScale));
        }

        /// <summary>播放循环音效。</summary>
        /// <param name="clip">音频 Clip。</param>
        /// <param name="volumeScale">音量缩放。</param>
        /// <returns>循环音效 id；clip 为 null 或没有可用音源时返回 0。</returns>
        public int PlayLoop(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null)
            {
                return 0;
            }

            var index = RentFreeOrOldestOneShot();
            if (index < 0)
            {
                GameLog.Warning(LogCategories.Audio, $"No free AudioSource for loop {LogStyle.Name(clip.name)}; all {PoolSize} are playing loops.");
                return 0;
            }

            var source = _pool[index];
            source.Stop();
            _reserved[index] = true;
            _oneShotClips[index] = null;
            source.transform.localPosition = Vector3.zero;
            source.spatialBlend = 0f;
            source.pitch = 1f;
            source.loop = true;
            source.clip = clip;
            source.volume = _volume.GetSfxVolume(volumeScale);
            source.Play();

            var id = _nextLoopId++;
            _loops[id] = new Loop { Source = source, VolumeScale = volumeScale };
            return id;
        }

        /// <summary>停止指定循环音效。</summary>
        /// <param name="loopId">由 <see cref="PlayLoop"/> 返回的 id。</param>
        public void StopLoop(int loopId)
        {
            if (loopId <= 0 || !_loops.TryGetValue(loopId, out var loop))
            {
                return;
            }

            _loops.Remove(loopId);
            ReleaseLoopSource(loop.Source);
        }

        /// <summary>停止全部循环音效。</summary>
        public void StopAllLoops()
        {
            foreach (var pair in _loops)
            {
                ReleaseLoopSource(pair.Value.Source);
            }

            _loops.Clear();
        }

        /// <summary>刷新全部循环音效音量（保留各自的音量缩放）。</summary>
        public void RefreshVolume()
        {
            foreach (var pair in _loops)
            {
                pair.Value.Source.volume = _volume.GetSfxVolume(pair.Value.VolumeScale);
            }
        }

        /// <summary>指定 Clip 是否正在本通道的某个音源上播放（循环音或尚未播完的一次性音效）。</summary>
        /// <param name="clip">音频 Clip。</param>
        /// <returns>正在使用返回 true。</returns>
        public bool IsUsing(AudioClip clip)
        {
            if (clip == null)
            {
                return false;
            }

            for (var i = 0; i < _pool.Count; i++)
            {
                var source = _pool[i];
                if (!source.isPlaying)
                {
                    continue;
                }

                if ((_reserved[i] && source.clip == clip) || _oneShotClips[i] == clip)
                {
                    return true;
                }
            }

            return false;
        }

        void MarkOneShot(int index, AudioClip clip)
        {
            _startedAt[index] = Time.realtimeSinceStartup;
            _oneShotClips[index] = clip;
        }

        void ReleaseLoopSource(AudioSource source)
        {
            source.Stop();
            source.clip = null;
            source.loop = false;
            var index = _pool.IndexOf(source);
            if (index >= 0)
            {
                _reserved[index] = false;
            }
        }

        /// <summary>一次性音效：优先空闲音源；都在响时停掉最早开始的一次性音源再复用。全是循环音时返回 -1。</summary>
        int RentOneShotSource()
        {
            var index = RentFreeOrOldestOneShot();
            if (index >= 0 && _pool[index].isPlaying)
            {
                _pool[index].Stop();
            }

            return index;
        }

        int RentFreeOrOldestOneShot()
        {
            for (var i = 0; i < _pool.Count; i++)
            {
                var index = (_roundRobin + i) % _pool.Count;
                if (!_reserved[index] && !_pool[index].isPlaying)
                {
                    _roundRobin = (index + 1) % _pool.Count;
                    return index;
                }
            }

            var oldest = -1;
            for (var i = 0; i < _pool.Count; i++)
            {
                if (_reserved[i])
                {
                    continue;
                }

                if (oldest < 0 || _startedAt[i] < _startedAt[oldest])
                {
                    oldest = i;
                }
            }

            return oldest;
        }

        static AudioSource CreateSource(Transform root, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            return source;
        }
    }
}
