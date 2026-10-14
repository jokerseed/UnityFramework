using System;
using UnityEngine;
using YooAsset;

namespace Framework.Res
{
    /// <summary>
    /// YooAsset <c>AssetHandle</c> 的只读包装，隐藏 YooAsset API，
    /// 提供类型安全的资源访问与 <see cref="IDisposable"/> 生命周期管理。
    /// </summary>
    public readonly struct ResourceAssetHandle : IDisposable
    {
        readonly AssetHandle _handle;

        /// <summary>使用 YooAsset 原生句柄构造包装。</summary>
        /// <param name="handle">YooAsset 原生资源句柄；可为 null（<see cref="IsValid"/> 将返回 false）。</param>
        public ResourceAssetHandle(AssetHandle handle)
        {
            _handle = handle;
        }

        /// <summary>句柄是否有效（非 null 且未被释放）。</summary>
        public bool IsValid => _handle != null && _handle.IsValid;

        /// <summary>加载是否已完成（包括失败情况）。</summary>
        public bool IsDone => _handle != null && _handle.IsDone;

        /// <summary>当前加载状态。</summary>
        public ResourceLoadStatus Status => MapStatus(_handle?.Status);

        /// <summary>加载是否成功。</summary>
        public bool Succeeded => Status == ResourceLoadStatus.Succeeded;

        /// <summary>失败时的错误信息；未失败或句柄无效则为空字符串。</summary>
        public string Error => _handle != null ? _handle.Error : string.Empty;

        /// <summary>原始 <see cref="UnityEngine.Object"/> 资源；加载未完成或失败则为 null。</summary>
        public UnityEngine.Object Asset => _handle?.AssetObject;

        /// <summary>以强类型方式获取资源对象。</summary>
        /// <typeparam name="T">目标资源类型，须继承 <see cref="UnityEngine.Object"/>。</typeparam>
        /// <returns>资源对象；句柄无效或类型不匹配则返回 null。</returns>
        public T GetAsset<T>() where T : UnityEngine.Object
        {
            return _handle != null ? _handle.GetAssetObject<T>() : null;
        }

        /// <summary>同步实例化资源为场景对象。</summary>
        /// <param name="parent">父 Transform；为 null 时实例化到场景根。</param>
        /// <param name="worldPositionStays">是否保持世界坐标；仅在 <paramref name="parent"/> 非 null 时有效。</param>
        /// <returns>实例化后的 <see cref="GameObject"/>；句柄无效则返回 null。</returns>
        public GameObject InstantiateSync(Transform parent = null, bool worldPositionStays = false)
        {
            if (_handle == null)
            {
                return null;
            }

            if (parent == null)
            {
                return _handle.InstantiateSync();
            }

            return _handle.InstantiateSync(new InstantiateOptions(true, parent, worldPositionStays));
        }

        /// <summary>释放底层 YooAsset 句柄，减少资源引用计数。</summary>
        public void Dispose()
        {
            _handle?.Release();
        }

        static ResourceLoadStatus MapStatus(EOperationStatus? status)
        {
            if (status == null)
            {
                return ResourceLoadStatus.None;
            }

            switch (status.Value)
            {
                case EOperationStatus.Processing:
                    return ResourceLoadStatus.Processing;
                case EOperationStatus.Succeeded:
                    return ResourceLoadStatus.Succeeded;
                case EOperationStatus.Failed:
                    return ResourceLoadStatus.Failed;
                default:
                    return ResourceLoadStatus.None;
            }
        }
    }
}
