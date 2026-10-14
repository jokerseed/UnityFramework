using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Framework.Logging;
using UnityEngine;

namespace Framework.Res
{
    /// <summary>
    /// 资源作用域：批量持有本域内加载的 <see cref="ResourceAssetHandle"/>，
    /// <see cref="Dispose"/> 时统一释放。约定 Scope 持 Asset，ObjectPool 持 Instance。
    /// </summary>
    public sealed class ResourceScope : IDisposable
    {
        readonly string _name;
        readonly List<ResourceAssetHandle> _handles = new List<ResourceAssetHandle>(8);
        bool _disposed;

        ResourceScope(string name)
        {
            _name = name ?? string.Empty;
        }

        /// <summary>作用域名称（日志用）。</summary>
        public string Name => _name;

        /// <summary>是否已释放。</summary>
        public bool IsDisposed => _disposed;

        /// <summary>当前登记的 Asset 句柄数量。</summary>
        public int HandleCount => _handles.Count;

        /// <summary>创建资源作用域。</summary>
        /// <param name="name">作用域名称，用于日志；可为 null 或空字符串。</param>
        /// <returns>新的作用域实例。</returns>
        public static ResourceScope Create(string name)
        {
            return new ResourceScope(name);
        }

        /// <summary>同步加载并登记句柄。失败时不登记；句柄仍可能有效，调用方须自行 <see cref="ResourceAssetHandle.Dispose"/>，并可读取 <see cref="ResourceAssetHandle.Error"/>。</summary>
        /// <typeparam name="T">资源类型，须继承 <see cref="UnityEngine.Object"/>。</typeparam>
        /// <param name="location">YooAsset 寻址字符串。</param>
        /// <returns>资源句柄。成功时由本作用域持有；失败时不持有，调用方负责释放。</returns>
        /// <exception cref="ObjectDisposedException">作用域已释放。</exception>
        /// <exception cref="InvalidOperationException"><see cref="ResourceManager"/> 尚未初始化。</exception>
        public ResourceAssetHandle LoadSync<T>(string location) where T : UnityEngine.Object
        {
            ThrowIfDisposed();
            var handle = ResourceManager.Instance.LoadAssetSync<T>(location);
            if (handle.IsValid && handle.Succeeded)
            {
                Track(handle);
            }

            return handle;
        }

        /// <summary>异步加载并登记句柄。</summary>
        /// <typeparam name="T">资源类型，须继承 <see cref="UnityEngine.Object"/>。</typeparam>
        /// <param name="location">YooAsset 寻址字符串。</param>
        /// <param name="priority">调度优先级，数值越大越先处理；默认 0。</param>
        /// <param name="cancellationToken">取消时撤销排队中的请求。</param>
        /// <returns>
        /// 资源句柄。成功时已由本作用域持有；失败时不登记，若仍然有效则由调用方 <see cref="ResourceAssetHandle.Dispose"/>。
        /// 等待期间作用域被释放时返回无效句柄。
        /// </returns>
        /// <exception cref="ObjectDisposedException">作用域已释放。</exception>
        /// <exception cref="InvalidOperationException"><see cref="ResourceManager"/> 尚未初始化。</exception>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> 已取消。</exception>
        public async UniTask<ResourceAssetHandle> LoadAsync<T>(
            string location,
            int priority = 0,
            CancellationToken cancellationToken = default) where T : UnityEngine.Object
        {
            ThrowIfDisposed();
            var loaded = await ResourceManager.Instance.LoadAssetAsync<T>(location, priority, cancellationToken);
            if (_disposed)
            {
                loaded.Dispose();
                return default;
            }

            if (loaded.IsValid && loaded.Succeeded)
            {
                Track(loaded);
            }

            return loaded;
        }

        /// <summary>
        /// 登记外部已成功加载的句柄，之后由本作用域统一 <see cref="Dispose"/>。
        /// 无效或失败句柄会被忽略。
        /// </summary>
        /// <param name="handle">要登记的句柄。</param>
        /// <exception cref="ObjectDisposedException">作用域已释放。</exception>
        public void Track(ResourceAssetHandle handle)
        {
            ThrowIfDisposed();
            if (!handle.IsValid || !handle.Succeeded)
            {
                return;
            }

            _handles.Add(handle);
        }

        /// <summary>释放本作用域登记的全部 Asset 句柄。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            for (var i = 0; i < _handles.Count; i++)
            {
                if (_handles[i].IsValid)
                {
                    _handles[i].Dispose();
                }
            }

            _handles.Clear();
            if (!string.IsNullOrEmpty(_name))
            {
                GameLog.Info(LogCategories.Resource, $"Scope {LogStyle.Name(_name)} {LogStyle.Muted("released")}");
            }
        }

        void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ResourceScope), _name);
            }
        }
    }
}
