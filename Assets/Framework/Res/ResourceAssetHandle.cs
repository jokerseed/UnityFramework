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
        public EOperationStatus Status => _handle != null ? _handle.Status : EOperationStatus.None;
        public string Error => _handle != null ? _handle.Error : string.Empty;
        public UnityEngine.Object Asset => _handle?.AssetObject;
        public AssetHandle RawHandle => _handle;

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
    }
}
