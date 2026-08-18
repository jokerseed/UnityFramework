using System;
using System.Collections.Generic;
using UnityEngine;
using YooAsset;

namespace Framework.Res
{
    /// <summary>
    /// 资源分帧调度器：Load / Instantiate / Unload 统一排队。
    /// 每帧按时间预算执行，个数上限仅作安全阀。
    /// </summary>
    sealed class ResourceScheduler
    {
        sealed class LoadItem
        {
            public ResourceRequestHandle Request;
            public string Location;
            public Type AssetType;
            public int Priority;
            public Action<ResourceAssetHandle> OnComplete;
            public AssetHandle YooHandle;
        }

        sealed class InstantiateItem
        {
            public ResourceRequestHandle Request;
            public ResourceAssetHandle AssetHandle;
            public Transform Parent;
            public bool WorldPositionStays;
            public int Priority;
            public Action<GameObject> OnComplete;
        }

        readonly ResourceSchedulerOptions _options;
        readonly Func<string, Type, AssetHandle> _startLoad;
        readonly Func<UnloadUnusedAssetsOperation> _startUnload;
        readonly List<LoadItem> _pendingLoads = new List<LoadItem>(16);
        readonly List<LoadItem> _inFlight = new List<LoadItem>(16);
        readonly List<LoadItem> _completedLoads = new List<LoadItem>(16);
        readonly List<InstantiateItem> _pendingInstantiates = new List<InstantiateItem>(16);
        readonly List<LoadItem> _inFlightScratch = new List<LoadItem>(16);

        int _nextId = 1;
        bool _unloadRequested;
        UnloadUnusedAssetsOperation _unloadOperation;
        bool _stopped;

        /// <summary>等待发起的加载数量。</summary>
        public int PendingLoadCount => _pendingLoads.Count;

        /// <summary>进行中的加载数量。</summary>
        public int InFlightCount => _inFlight.Count;

        /// <summary>等待实例化的数量。</summary>
        public int PendingInstantiateCount => _pendingInstantiates.Count;

        /// <summary>是否有待处理或进行中的 Unload。</summary>
        public bool HasPendingUnload => _unloadRequested || _unloadOperation != null;

        public ResourceScheduler(
            ResourceSchedulerOptions options,
            Func<string, Type, AssetHandle> startLoad,
            Func<UnloadUnusedAssetsOperation> startUnload)
        {
            _options = options ?? new ResourceSchedulerOptions();
            _startLoad = startLoad ?? throw new ArgumentNullException(nameof(startLoad));
            _startUnload = startUnload ?? throw new ArgumentNullException(nameof(startUnload));
        }

        /// <summary>入队异步加载。</summary>
        public ResourceRequestHandle EnqueueLoad(
            string location,
            Type assetType,
            Action<ResourceAssetHandle> onComplete,
            int priority)
        {
            var request = CreateRequest();
            var item = new LoadItem
            {
                Request = request,
                Location = location,
                AssetType = assetType,
                Priority = priority,
                OnComplete = onComplete,
            };
            InsertByPriority(_pendingLoads, item, i => i.Priority);
            return request;
        }

        /// <summary>入队实例化。</summary>
        public ResourceRequestHandle EnqueueInstantiate(
            ResourceAssetHandle handle,
            Transform parent,
            bool worldPositionStays,
            Action<GameObject> onComplete,
            int priority)
        {
            var request = CreateRequest();
            var item = new InstantiateItem
            {
                Request = request,
                AssetHandle = handle,
                Parent = parent,
                WorldPositionStays = worldPositionStays,
                Priority = priority,
                OnComplete = onComplete,
            };
            InsertByPriority(_pendingInstantiates, item, i => i.Priority);
            return request;
        }

        /// <summary>合并一次 UnloadUnusedAssets 请求。</summary>
        public void RequestUnloadUnusedAssets()
        {
            if (_stopped)
            {
                return;
            }

            _unloadRequested = true;
        }

        /// <summary>取消指定请求。</summary>
        public void Cancel(ResourceRequestHandle request)
        {
            if (request == null || request.IsDone || _stopped)
            {
                return;
            }

            if (RemovePendingLoad(request, disposeYooHandle: false))
            {
                CompleteCancelled(request);
                return;
            }

            for (var i = 0; i < _inFlight.Count; i++)
            {
                if (_inFlight[i].Request == request)
                {
                    var item = _inFlight[i];
                    _inFlight.RemoveAt(i);
                    ReleaseYooHandle(item);
                    CompleteCancelled(request);
                    return;
                }
            }

            if (RemovePendingInstantiate(request))
            {
                CompleteCancelled(request);
            }
        }

        /// <summary>每帧推进队列。</summary>
        public void Tick()
        {
            if (_stopped)
            {
                return;
            }

            var frameStart = Time.realtimeSinceStartup;
            PollInFlight();
            StartPendingLoads(frameStart);
            ProcessInstantiates(frameStart);
            DispatchLoadCompletes(frameStart);
            TryStartUnload(frameStart);
            PollUnload();
        }

        /// <summary>取消全部未完成请求并清空队列。</summary>
        public void Shutdown()
        {
            _stopped = true;
            CancelList(_pendingLoads, disposeYooHandle: false);
            CancelList(_inFlight, disposeYooHandle: true);
            CancelList(_completedLoads, disposeYooHandle: true);

            for (var i = 0; i < _pendingInstantiates.Count; i++)
            {
                CompleteCancelled(_pendingInstantiates[i].Request);
            }

            _pendingInstantiates.Clear();
            _unloadRequested = false;
            _unloadOperation = null;
        }

        ResourceRequestHandle CreateRequest()
        {
            var request = new ResourceRequestHandle
            {
                Id = _nextId++,
                Status = ResourceRequestStatus.Pending,
                Scheduler = this,
            };
            return request;
        }

        void StartPendingLoads(float frameStart)
        {
            var started = 0;
            while (_pendingLoads.Count > 0
                   && started < _options.MaxLoadStartsPerFrame
                   && _inFlight.Count < _options.MaxLoadInFlight
                   && !Exceeded(frameStart, _options.MaxFrameBudgetMs))
            {
                var item = DequeueFirst(_pendingLoads);
                if (item.Request.Status == ResourceRequestStatus.Cancelled)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(item.Location) || item.AssetType == null)
                {
                    FailLoad(item, "Invalid location or type.");
                    continue;
                }

                try
                {
                    item.YooHandle = _startLoad(item.Location, item.AssetType);
                }
                catch (Exception ex)
                {
                    FailLoad(item, ex.Message);
                    continue;
                }

                item.Request.Status = ResourceRequestStatus.Processing;
                _inFlight.Add(item);
                started++;
            }
        }

        void PollInFlight()
        {
            _inFlightScratch.Clear();
            for (var i = 0; i < _inFlight.Count; i++)
            {
                _inFlightScratch.Add(_inFlight[i]);
            }

            for (var i = 0; i < _inFlightScratch.Count; i++)
            {
                var item = _inFlightScratch[i];
                if (item.YooHandle == null || !item.YooHandle.IsValid)
                {
                    _inFlight.Remove(item);
                    FailLoad(item, "YooAsset handle is invalid.");
                    continue;
                }

                if (!item.YooHandle.IsDone)
                {
                    continue;
                }

                _inFlight.Remove(item);
                _completedLoads.Add(item);
            }
        }

        void DispatchLoadCompletes(float frameStart)
        {
            var dispatched = 0;
            var categoryStart = Time.realtimeSinceStartup;
            while (_completedLoads.Count > 0
                   && dispatched < _options.MaxCallbacksPerFrame
                   && !Exceeded(frameStart, _options.MaxFrameBudgetMs)
                   && !Exceeded(categoryStart, _options.CallbackBudgetMs))
            {
                var item = DequeueFirst(_completedLoads);
                if (item.Request.Status == ResourceRequestStatus.Cancelled)
                {
                    ReleaseYooHandle(item);
                    continue;
                }

                var wrapped = new ResourceAssetHandle(item.YooHandle);
                if (!wrapped.IsValid || !wrapped.Succeeded)
                {
                    var error = wrapped.Error;
                    FailLoad(item, string.IsNullOrEmpty(error) ? "Load failed." : error);
                    dispatched++;
                    continue;
                }

                item.Request.AssetHandle = wrapped;
                item.Request.Status = ResourceRequestStatus.Succeeded;
                item.OnComplete?.Invoke(wrapped);
                dispatched++;
            }
        }

        void ProcessInstantiates(float frameStart)
        {
            var processed = 0;
            var categoryStart = Time.realtimeSinceStartup;
            while (_pendingInstantiates.Count > 0
                   && processed < _options.MaxInstantiatesPerFrame
                   && !Exceeded(frameStart, _options.MaxFrameBudgetMs)
                   && (processed == 0 || !Exceeded(categoryStart, _options.InstantiateBudgetMs)))
            {
                var item = DequeueFirst(_pendingInstantiates);
                if (item.Request.Status == ResourceRequestStatus.Cancelled)
                {
                    continue;
                }

                item.Request.Status = ResourceRequestStatus.Processing;
                GameObject instance = null;
                try
                {
                    instance = item.AssetHandle.InstantiateSync(item.Parent, item.WorldPositionStays);
                }
                catch (Exception ex)
                {
                    item.Request.Status = ResourceRequestStatus.Failed;
                    item.Request.Error = ex.Message;
                    item.OnComplete?.Invoke(null);
                    processed++;
                    continue;
                }

                if (instance == null)
                {
                    item.Request.Status = ResourceRequestStatus.Failed;
                    item.Request.Error = "Instantiate returned null.";
                    item.OnComplete?.Invoke(null);
                }
                else
                {
                    item.Request.Instance = instance;
                    item.Request.Status = ResourceRequestStatus.Succeeded;
                    item.OnComplete?.Invoke(instance);
                }

                processed++;
            }
        }

        void TryStartUnload(float frameStart)
        {
            if (!_unloadRequested || _unloadOperation != null)
            {
                return;
            }

            if (_options.MaxUnloadPerFrame <= 0)
            {
                return;
            }

            if (_options.UnloadOnlyWhenIdle
                && (_pendingLoads.Count > 0
                    || _inFlight.Count > 0
                    || _completedLoads.Count > 0
                    || _pendingInstantiates.Count > 0))
            {
                return;
            }

            if (Exceeded(frameStart, _options.MaxFrameBudgetMs))
            {
                return;
            }

            _unloadRequested = false;
            _unloadOperation = _startUnload();
        }

        void PollUnload()
        {
            if (_unloadOperation == null)
            {
                return;
            }

            if (!_unloadOperation.IsDone)
            {
                return;
            }

            _unloadOperation = null;
        }

        void FailLoad(LoadItem item, string error)
        {
            ReleaseYooHandle(item);
            item.Request.Status = ResourceRequestStatus.Failed;
            item.Request.Error = error ?? string.Empty;
            item.OnComplete?.Invoke(default);
        }

        void CompleteCancelled(ResourceRequestHandle request)
        {
            request.Status = ResourceRequestStatus.Cancelled;
            request.Error = "Cancelled.";
        }

        bool RemovePendingLoad(ResourceRequestHandle request, bool disposeYooHandle)
        {
            for (var i = 0; i < _pendingLoads.Count; i++)
            {
                if (_pendingLoads[i].Request != request)
                {
                    continue;
                }

                var item = _pendingLoads[i];
                _pendingLoads.RemoveAt(i);
                if (disposeYooHandle)
                {
                    ReleaseYooHandle(item);
                }

                return true;
            }

            return false;
        }

        bool RemovePendingInstantiate(ResourceRequestHandle request)
        {
            for (var i = 0; i < _pendingInstantiates.Count; i++)
            {
                if (_pendingInstantiates[i].Request != request)
                {
                    continue;
                }

                _pendingInstantiates.RemoveAt(i);
                return true;
            }

            return false;
        }

        void CancelList(List<LoadItem> list, bool disposeYooHandle)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (disposeYooHandle)
                {
                    ReleaseYooHandle(list[i]);
                }

                CompleteCancelled(list[i].Request);
            }

            list.Clear();
        }

        static void ReleaseYooHandle(LoadItem item)
        {
            if (item.YooHandle != null && item.YooHandle.IsValid)
            {
                item.YooHandle.Release();
            }

            item.YooHandle = null;
        }

        static T DequeueFirst<T>(List<T> list)
        {
            var item = list[0];
            list.RemoveAt(0);
            return item;
        }

        static void InsertByPriority<T>(List<T> list, T item, Func<T, int> getPriority)
        {
            var priority = getPriority(item);
            for (var i = 0; i < list.Count; i++)
            {
                if (priority > getPriority(list[i]))
                {
                    list.Insert(i, item);
                    return;
                }
            }

            list.Add(item);
        }

        static bool Exceeded(float start, float budgetMs)
        {
            return (Time.realtimeSinceStartup - start) * 1000f >= budgetMs;
        }
    }
}
