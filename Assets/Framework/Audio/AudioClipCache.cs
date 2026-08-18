using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Logging;
using Framework.Res;
using UnityEngine;

namespace Framework.Audio
{
    /// <summary>音频 Clip 缓存：按寻址字符串持有 <see cref="ResourceAssetHandle"/>。</summary>
    sealed class AudioClipCache
    {
        readonly Dictionary<string, ResourceAssetHandle> _handles =
            new Dictionary<string, ResourceAssetHandle>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> _loading =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int _generation;

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
                yield return ResourceManager.Instance.LoadAssetAsync<AudioClip>(location, h => handle = h);
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
            _loading.Clear();
        }

        bool TryGetCached(string location, out AudioClip clip)
        {
            if (_handles.TryGetValue(location, out var existing) && existing.IsValid && existing.Succeeded)
            {
                clip = existing.GetAsset<AudioClip>();
                return clip != null;
            }

            clip = null;
            return false;
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
        }
    }
}
