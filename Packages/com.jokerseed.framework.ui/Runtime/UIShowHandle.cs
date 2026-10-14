using System;
using Framework.Coroutine;
using Framework.Logging;
using Framework.Res;
using UnityEngine;

namespace Framework.UI
{
    /// <summary>
    /// 异步打开窗口的句柄：可查询是否仍在进行，并取消尚未完成的打开。
    /// <see cref="Stop"/> / <see cref="Dispose"/> 与 <see cref="Cancel"/> 等价。
    /// </summary>
    public sealed class UIShowHandle : ICoroutineHandle
    {
        readonly Type _windowType;
        readonly Action _onIncomplete;
        readonly Action<UIShowHandle> _onDetached;

        ICoroutineHandle _coroutine;
        ResourceRequestHandle _request;
        ResourceAssetHandle _assetHandle;
        GameObject _instance;
        bool _windowBound;
        bool _finished;

        internal UIShowHandle(Type windowType, Action onIncomplete, Action<UIShowHandle> onDetached)
        {
            _windowType = windowType;
            _onIncomplete = onIncomplete;
            _onDetached = onDetached;
        }

        /// <summary>已结束且未真正打开时使用的空句柄（已打开窗口走刷新）。</summary>
        internal static UIShowHandle Settled { get; } = new UIShowHandle(null, null, null)
        {
            _finished = true,
        };

        /// <summary>目标窗口类型；已结束的空句柄为 null。</summary>
        internal Type WindowType => _windowType;

        /// <summary>是否已取消。</summary>
        public bool IsCancelled { get; private set; }

        /// <inheritdoc />
        public bool IsRunning => !_finished && !IsCancelled && _coroutine != null && _coroutine.IsRunning;

        /// <summary>取消尚未完成的打开：停止协程、取消资源请求，并释放未绑定的资源与实例。</summary>
        public void Cancel()
        {
            if (_finished || IsCancelled)
            {
                return;
            }

            IsCancelled = true;
            if (_windowType != null)
            {
                GameLog.Info(LogCategories.UI, $"ShowAsync {LogStyle.Name(_windowType.Name)} {LogStyle.Muted("cancelled")}");
            }

            AbortIncomplete(invokeCallback: true);
        }

        /// <inheritdoc />
        public void Stop() => Cancel();

        /// <inheritdoc />
        public void Dispose() => Cancel();

        internal void BindCoroutine(ICoroutineHandle coroutine)
        {
            _coroutine = coroutine;
        }

        internal void SetRequest(ResourceRequestHandle request)
        {
            _request = request;
        }

        internal void SetAssetHandle(ResourceAssetHandle handle)
        {
            if (IsCancelled || _finished)
            {
                if (handle.IsValid)
                {
                    handle.Dispose();
                }

                return;
            }

            _assetHandle = handle;
        }

        internal void SetInstance(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (IsCancelled || _finished)
            {
                UnityEngine.Object.Destroy(instance);
                return;
            }

            _instance = instance;
        }

        internal void MarkBound()
        {
            _windowBound = true;
            _finished = true;
            _request = null;
            _instance = null;
            _assetHandle = default;
            _onDetached?.Invoke(this);
        }

        internal void MarkFailed()
        {
            if (_finished || IsCancelled)
            {
                return;
            }

            _finished = true;
            ReleaseUnboundResources();
            _onDetached?.Invoke(this);
        }

        internal void AbortIfStillIncomplete()
        {
            if (_finished || IsCancelled || _windowBound)
            {
                return;
            }

            IsCancelled = true;
            AbortIncomplete(invokeCallback: true);
        }

        void AbortIncomplete(bool invokeCallback)
        {
            _finished = true;
            _coroutine?.Stop();
            _coroutine = null;
            ReleaseUnboundResources();
            _onDetached?.Invoke(this);
            if (invokeCallback)
            {
                _onIncomplete?.Invoke();
            }
        }

        void ReleaseUnboundResources()
        {
            if (_windowBound)
            {
                return;
            }

            var instance = _instance != null ? _instance : _request?.Instance;
            var asset = _assetHandle.IsValid ? _assetHandle : (_request != null ? _request.AssetHandle : default);

            _request?.Cancel();

            if (instance != null)
            {
                UnityEngine.Object.Destroy(instance);
            }

            _instance = null;

            if (asset.IsValid)
            {
                asset.Dispose();
            }

            _assetHandle = default;
            _request = null;
        }
    }
}
