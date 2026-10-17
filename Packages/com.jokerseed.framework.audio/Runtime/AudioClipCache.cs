using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Logging;
using Framework.Res;
using UnityEngine;

namespace Framework.Audio
{
    /// <summary>
    /// 音频 Clip 缓存：按寻址字符串持有 <see cref="ResourceAssetHandle"/>。
    /// 超过 <see cref="Capacity"/> 时按最近最少使用淘汰；<see cref="IsInUse"/> 判定仍在播放的 Clip 不淘汰。
    /// </summary>
    sealed class AudioClipCache
    {
        /// <summary>默认最多缓存的 Clip 数。</summary>
        public const int DefaultCapacity = 64;

        readonly Dictionary<string, ResourceAssetHandle> _handles =
            new Dictionary<string, ResourceAssetHandle>(StringComparer.Ordinal);
        readonly Dictionary<string, long> _lastUse = new Dictionary<string, long>(StringComparer.Ordinal);
        readonly HashSet<string> _loading =
            new HashSet<string>(StringComparer.Ordinal);

        int _generation;
        long _useCounter;
        int _capacity = DefaultCapacity;

        /// <summary>最多缓存的 Clip 数（至少 1）；调小时立即按 LRU 淘汰。</summary>
        public int Capacity
        {
            get => _capacity;
            set
            {
                _capacity = Math.Max(1, value);
                Trim();
            }
        }

        /// <summary>判断 Clip 是否仍在某个音源上播放；为 null 时视为都不在用。</summary>
        public Func<AudioClip, bool> IsInUse { get; set; }

        /// <summary>当前缓存的 Clip 数。</summary>
        public int Count => _handles.Count;

        /// <summary>同步获取或加载 Clip。</summary>
        /// <param name="location">YooAsset 寻址字符串。</param>
        /// <returns>加载成功返回 Clip；失败返回 null。</returns>
        public AudioClip GetOrLoadSync(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return null;
            }

            if (TryGetCached(location, out var clip))
            {
                return clip;
            }

            DisposeInvalid(location);

            var handle = ResourceManager.Instance.LoadAssetSync<AudioClip>(location);
            if (!handle.IsValid || !handle.Succeeded)
            {
                GameLog.Error(LogCategories.Audio,
                    $"Load clip failed: {LogStyle.Name(location)}  error={LogStyle.Value(handle.Error)}");
                handle.Dispose();
                return null;
            }

            StoreHandle(location, handle);
            return TryGetCached(location, out clip) ? clip : null;
        }

        /// <summary>异步加载 Clip。同一 location 正在加载时后来者等待第一次完成，不另开句柄。</summary>
        /// <param name="location">YooAsset 寻址字符串。</param>
        /// <param name="onComplete">完成回调；参数为 Clip 或失败时的 null。</param>
        /// <returns>协程迭代器。</returns>
        public IEnumerator LoadAsync(string location, Action<AudioClip> onComplete)
        {
            if (string.IsNullOrEmpty(location))
            {
                onComplete?.Invoke(null);
                yield break;
            }

            if (TryGetCached(location, out var cached))
            {
                onComplete?.Invoke(cached);
                yield break;
            }

            if (_loading.Contains(location))
            {
                while (_loading.Contains(location))
                {
                    if (TryGetCached(location, out cached))
                    {
                        onComplete?.Invoke(cached);
                        yield break;
                    }

                    yield return null;
                }

                onComplete?.Invoke(TryGetCached(location, out cached) ? cached : null);
                yield break;
            }

            var generation = _generation;
            _loading.Add(location);
            ResourceAssetHandle handle = default;
            try
            {
                yield return ResourceManager.Instance.LoadAssetScheduled<AudioClip>(location, h => handle = h);
                if (generation != _generation)
                {
                    handle.Dispose();
                    onComplete?.Invoke(null);
                    yield break;
                }

                if (handle.IsValid && handle.Succeeded)
                {
                    if (TryGetCached(location, out cached))
                    {
                        handle.Dispose();
                        onComplete?.Invoke(cached);
                    }
                    else
                    {
                        StoreHandle(location, handle);
                        onComplete?.Invoke(handle.GetAsset<AudioClip>());
                    }
                }
                else
                {
                    GameLog.Error(LogCategories.Audio,
                        $"Load clip async failed: {LogStyle.Name(location)}  error={LogStyle.Value(handle.Error)}");
                    handle.Dispose();
                    onComplete?.Invoke(null);
                }
            }
            finally
            {
                _loading.Remove(location);
            }
        }

        /// <summary>释放全部缓存句柄。</summary>
        public void ReleaseAll()
        {
            _generation++;
            foreach (var pair in _handles)
            {
                pair.Value.Dispose();
            }

            _handles.Clear();
            _lastUse.Clear();
            _loading.Clear();
        }

        bool TryGetCached(string location, out AudioClip clip)
        {
            if (_handles.TryGetValue(location, out var existing) && existing.IsValid && existing.Succeeded)
            {
                clip = existing.GetAsset<AudioClip>();
                if (clip != null)
                {
                    _lastUse[location] = ++_useCounter;
                    return true;
                }

                return false;
            }

            clip = null;
            return false;
        }

        /// <summary>超过容量时淘汰最久未用、且不在播放中的 Clip。</summary>
        void Trim()
        {
            while (_handles.Count > _capacity)
            {
                string victim = null;
                var oldest = long.MaxValue;
                foreach (var pair in _handles)
                {
                    _lastUse.TryGetValue(pair.Key, out var used);
                    if (used >= oldest || _loading.Contains(pair.Key))
                    {
                        continue;
                    }

                    var clip = pair.Value.IsValid ? pair.Value.GetAsset<AudioClip>() : null;
                    if (clip != null && IsInUse != null && IsInUse(clip))
                    {
                        continue;
                    }

                    oldest = used;
                    victim = pair.Key;
                }

                if (victim == null)
                {
                    // 剩下的都在播放：暂时超出容量，等它们播完后的下一次写入再淘汰。
                    return;
                }

                var evicted = _handles[victim];
                if (evicted.IsValid)
                {
                    evicted.Dispose();
                }

                _handles.Remove(victim);
                _lastUse.Remove(victim);
            }
        }

        void DisposeInvalid(string location)
        {
            if (!_handles.TryGetValue(location, out var existing))
            {
                return;
            }

            if (existing.IsValid && existing.Succeeded)
            {
                return;
            }

            if (existing.IsValid)
            {
                existing.Dispose();
            }

            _handles.Remove(location);
            _lastUse.Remove(location);
        }

        void StoreHandle(string location, ResourceAssetHandle handle)
        {
            DisposeInvalid(location);
            if (TryGetCached(location, out _))
            {
                handle.Dispose();
                return;
            }

            _handles[location] = handle;
            _lastUse[location] = ++_useCounter;
            Trim();
        }
    }
}
