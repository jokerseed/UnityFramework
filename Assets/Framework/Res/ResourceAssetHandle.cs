using System;
using UnityEngine;
using YooAsset;

namespace Framework.Res
{
    public readonly struct ResourceAssetHandle : IDisposable
    {
        readonly AssetHandle _handle;

        public ResourceAssetHandle(AssetHandle handle)
        {
            _handle = handle;
        }

        public bool IsValid => _handle != null && _handle.IsValid;
        public bool IsDone => _handle != null && _handle.IsDone;
        public ResourceLoadStatus Status => MapStatus(_handle?.Status);
        public bool Succeeded => Status == ResourceLoadStatus.Succeeded;
        public string Error => _handle != null ? _handle.Error : string.Empty;
        public UnityEngine.Object Asset => _handle?.AssetObject;

        public T GetAsset<T>() where T : UnityEngine.Object
        {
            return _handle != null ? _handle.GetAssetObject<T>() : null;
        }

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
