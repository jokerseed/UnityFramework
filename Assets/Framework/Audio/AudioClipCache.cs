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

        /// <summary>同步获取或加载 Clip。</summary>
        /// <param name="location">YooAsset 寻址字符串。</param>
        /// <returns>加载成功返回 Clip；失败返回 null。</returns>
        public AudioClip GetOrLoadSync(string location)
        {
            if (string.IsNullOrEmpty(location))
            {
                return null;
            }

            if (_handles.TryGetValue(location, out var existing) && existing.IsValid && existing.Succeeded)
            {
                return existing.GetAsset<AudioClip>();
            }

            if (existing.IsValid)
            {
                existing.Dispose();
                _handles.Remove(location);
            }

            var handle = ResourceManager.Instance.LoadAssetSync<AudioClip>(location);
            if (!handle.IsValid || !handle.Succeeded)
            {
                GameLog.Error(LogCategories.Audio,
                    $"Load clip failed: {LogStyle.Name(location)}  error={LogStyle.Value(handle.Error)}");
                handle.Dispose();
                return null;
            }

            _handles[location] = handle;
            return handle.GetAsset<AudioClip>();
        }

        /// <summary>异步加载 Clip。</summary>
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

            if (_handles.TryGetValue(location, out var existing) && existing.IsValid && existing.Succeeded)
            {
                onComplete?.Invoke(existing.GetAsset<AudioClip>());
                yield break;
            }

            if (existing.IsValid)
            {
                existing.Dispose();
                _handles.Remove(location);
            }

            ResourceAssetHandle handle = default;
            yield return ResourceManager.Instance.LoadAssetAsync<AudioClip>(location, h => handle = h);
            if (handle.IsValid && handle.Succeeded)
            {
                _handles[location] = handle;
                onComplete?.Invoke(handle.GetAsset<AudioClip>());
            }
            else
            {
                GameLog.Error(LogCategories.Audio,
                    $"Load clip async failed: {LogStyle.Name(location)}  error={LogStyle.Value(handle.Error)}");
                handle.Dispose();
                onComplete?.Invoke(null);
            }
        }

        /// <summary>释放全部缓存句柄。</summary>
        public void ReleaseAll()
        {
            foreach (var pair in _handles)
            {
                pair.Value.Dispose();
            }

            _handles.Clear();
        }
    }
}
