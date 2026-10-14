using System.Collections.Generic;
using UnityEngine;

namespace Framework.Audio
{
    /// <summary>SFX 通道：AudioSource 对象池，支持 OneShot 与循环音效。</summary>
    sealed class SfxChannel
    {
        readonly List<AudioSource> _pool = new List<AudioSource>(16);
        readonly Dictionary<int, AudioSource> _loops = new Dictionary<int, AudioSource>(8);
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

            var source = RentSource();
            source.transform.localPosition = Vector3.zero;
            source.spatialBlend = 0f;
            source.pitch = pitch;
            source.loop = false;
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

            var source = RentSource();
            source.transform.position = worldPosition;
            source.spatialBlend = 1f;
            source.pitch = pitch;
            source.loop = false;
            source.PlayOneShot(clip, _volume.GetSfxVolume(volumeScale));
        }

        /// <summary>播放循环音效。</summary>
        /// <param name="clip">音频 Clip。</param>
        /// <param name="volumeScale">音量缩放。</param>
        /// <returns>循环音效 id；clip 为 null 时返回 0。</returns>
        public int PlayLoop(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null)
            {
                return 0;
            }

            var source = RentSource();
            source.transform.localPosition = Vector3.zero;
            source.spatialBlend = 0f;
            source.loop = true;
            source.clip = clip;
            source.volume = _volume.GetSfxVolume(volumeScale);
            source.Play();

            var id = _nextLoopId++;
            _loops[id] = source;
            return id;
        }

        /// <summary>停止指定循环音效。</summary>
        /// <param name="loopId">由 <see cref="PlayLoop"/> 返回的 id。</param>
        public void StopLoop(int loopId)
        {
            if (loopId <= 0 || !_loops.TryGetValue(loopId, out var source))
            {
                return;
            }

            source.Stop();
            source.clip = null;
            source.loop = false;
            _loops.Remove(loopId);
        }

        /// <summary>停止全部循环音效。</summary>
        public void StopAllLoops()
        {
            foreach (var pair in _loops)
            {
                pair.Value.Stop();
                pair.Value.clip = null;
                pair.Value.loop = false;
            }

            _loops.Clear();
        }

        /// <summary>刷新全部循环音效音量。</summary>
        public void RefreshVolume()
        {
            var volume = _volume.GetSfxVolume();
            foreach (var pair in _loops)
            {
                pair.Value.volume = volume;
            }
        }

        AudioSource RentSource()
        {
            for (var i = 0; i < _pool.Count; i++)
            {
                var index = (_roundRobin + i) % _pool.Count;
                var source = _pool[index];
                if (!source.isPlaying)
                {
                    _roundRobin = (index + 1) % _pool.Count;
                    return source;
                }
            }

            _roundRobin = (_roundRobin + 1) % _pool.Count;
            return _pool[_roundRobin];
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
