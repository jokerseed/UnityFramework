using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Core;
using Framework.Coroutine;
using Framework.Logging;
using Framework.Res;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Framework.UI
{
    /// <summary>
    /// UI 管理器：维护层级根节点、窗口栈、同步/异步打开与资源生命周期。
    /// 参考 TEngine UIModule 设计，纯 C# 驱动窗口生命周期。
    /// </summary>
    public sealed class UIManager : PersistentSingleton<UIManager>
    {
        const int LayerOrderStep = 50;
        const int WindowOrderStep = 10;

        readonly List<UIWindow> _stack = new List<UIWindow>(16);
        readonly Dictionary<Type, UIWindow> _opened = new Dictionary<Type, UIWindow>(16);
        readonly Dictionary<Type, UIWindow> _cached = new Dictionary<Type, UIWindow>(8);
        readonly Dictionary<Type, UIShowHandle> _pendingShows = new Dictionary<Type, UIShowHandle>(8);
        readonly Dictionary<Type, ICoroutineHandle> _delayUnloadTimers = new Dictionary<Type, ICoroutineHandle>(4);
        readonly Dictionary<UILayer, Transform> _layerRoots = new Dictionary<UILayer, Transform>(8);

        Transform _uiRoot;
        Transform _eventSystemRoot;
        bool _initialized;

        /// <summary>当前打开的窗口数量。</summary>
        public int OpenCount => _stack.Count;

        /// <summary>初始化 UI 根节点与五层 Canvas。</summary>
        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _uiRoot = new GameObject("UIRoot").transform;
            _uiRoot.SetParent(transform, false);
            _uiRoot.localPosition = Vector3.zero;
            _uiRoot.localRotation = Quaternion.identity;
            _uiRoot.localScale = Vector3.one;

            EnsureLayer(UILayer.Bottom);
            EnsureLayer(UILayer.UI);
            EnsureLayer(UILayer.Top);
            EnsureLayer(UILayer.Tips);
            EnsureLayer(UILayer.System);
            EnsureEventSystem();

            _initialized = true;
            GameLog.Info(LogCategories.UI, LogStyle.Ok("ready"));
        }

        /// <summary>同步打开窗口；若已打开则仅刷新。会取消同类型尚未完成的 <see cref="ShowAsync{TWindow}"/>。</summary>
        /// <typeparam name="TWindow">窗口类型，须有无参构造。</typeparam>
        /// <param name="userData">传入窗口的用户数据；可为 null。</param>
        /// <returns>打开的窗口实例。</returns>
        /// <exception cref="InvalidOperationException">资源加载或实例化失败。</exception>
        public TWindow Show<TWindow>(object userData = null) where TWindow : UIWindow, new()
        {
            EnsureInitialized();

            var windowType = typeof(TWindow);
            CancelPendingShow(windowType);
            CancelDelayedUnload(windowType);
            if (TryReviveCached<TWindow>(windowType, userData, out var revived))
            {
                return revived;
            }

            if (_opened.TryGetValue(windowType, out var existing))
            {
                existing.UserData = userData;
                existing.OnRefresh();
                BringToTop(existing);
                RefreshVisibility();
                return (TWindow)existing;
            }

            var metadata = ResolveMetadata(windowType);
            var loadHandle = ResourceManager.Instance.LoadAssetSync<GameObject>(metadata.Location);
            if (!loadHandle.IsValid || !loadHandle.Succeeded)
            {
                loadHandle.Dispose();
                throw new InvalidOperationException(
                    $"[UI] Load window failed: {windowType.Name}, location={metadata.Location}, error={loadHandle.Error}");
            }

            var window = CreateWindowInstance<TWindow>(loadHandle, metadata, userData);
            window.AssetHandle = loadHandle;
            return window;
        }

        /// <summary>异步打开窗口。调用返回句柄的 <see cref="UIShowHandle.Cancel"/> 可中止尚未完成的打开。</summary>
        /// <typeparam name="TWindow">窗口类型，须有无参构造。</typeparam>
        /// <param name="userData">传入窗口的用户数据；可为 null。</param>
        /// <param name="onComplete">完成回调；成功为窗口实例，失败或取消为 null。</param>
        /// <returns>异步打开句柄；窗口已打开时返回已结束的空句柄。</returns>
        public UIShowHandle ShowAsync<TWindow>(object userData = null, Action<TWindow> onComplete = null)
            where TWindow : UIWindow, new()
        {
            EnsureInitialized();

            var windowType = typeof(TWindow);
            CancelDelayedUnload(windowType);
            if (TryReviveCached<TWindow>(windowType, userData, out var cached))
            {
                onComplete?.Invoke(cached);
                return UIShowHandle.Settled;
            }

            if (_opened.TryGetValue(windowType, out var existing))
            {
                existing.UserData = userData;
                existing.OnRefresh();
                BringToTop(existing);
                RefreshVisibility();
                onComplete?.Invoke((TWindow)existing);
                return UIShowHandle.Settled;
            }

            CancelPendingShow(windowType);

            var operation = new UIShowHandle(
                windowType,
                () => onComplete?.Invoke(null),
                DetachPendingShow);
            _pendingShows[windowType] = operation;
            operation.BindCoroutine(GameCoroutine.StartGlobal(ShowAsyncCoroutine(operation, userData, onComplete)));
            return operation;
        }

        /// <summary>关闭指定类型窗口；若该类型仍在异步打开中则一并取消。</summary>
        /// <typeparam name="TWindow">窗口类型。</typeparam>
        /// <returns>关闭了已打开窗口或取消了进行中的异步打开时返回 true。</returns>
        public bool Close<TWindow>() where TWindow : UIWindow
        {
            return Close(typeof(TWindow));
        }

        /// <summary>关闭指定类型窗口；若该类型仍在异步打开中则一并取消。</summary>
        /// <param name="windowType">窗口类型，不可为 null。</param>
        /// <returns>关闭了已打开窗口或取消了进行中的异步打开时返回 true。</returns>
        public bool Close(Type windowType)
        {
            if (windowType == null)
            {
                return false;
            }

            var cancelledPending = CancelPendingShow(windowType);
            if (!_opened.TryGetValue(windowType, out var window))
            {
                return cancelledPending;
            }

            CloseWindow(window);
            return true;
        }

        /// <summary>
        /// 强制销毁指定类型窗口（忽略 <see cref="UIReleasePolicy"/> 缓存/延迟卸载）。
        /// </summary>
        /// <typeparam name="TWindow">窗口类型。</typeparam>
        /// <returns>销毁了已打开或已缓存实例时返回 true。</returns>
        public bool ForceDestroy<TWindow>() where TWindow : UIWindow
        {
            return ForceDestroy(typeof(TWindow));
        }

        /// <summary>
        /// 强制销毁指定类型窗口（忽略 <see cref="UIReleasePolicy"/> 缓存/延迟卸载）。
        /// </summary>
        /// <param name="windowType">窗口类型，不可为 null。</param>
        /// <returns>销毁了已打开或已缓存实例时返回 true。</returns>
        public bool ForceDestroy(Type windowType)
        {
            if (windowType == null)
            {
                return false;
            }

            var cancelledPending = CancelPendingShow(windowType);
            CancelDelayedUnload(windowType);

            var destroyed = false;
            if (_opened.TryGetValue(windowType, out var opened))
            {
                DestroyWindow(opened, force: true);
                destroyed = true;
            }

            if (_cached.TryGetValue(windowType, out var cached))
            {
                DestroyWindow(cached, force: true);
                destroyed = true;
            }

            return destroyed || cancelledPending;
        }

        /// <summary>关闭全部已打开窗口，并取消尚未完成的异步打开；各窗口按自身释放策略处理。</summary>
        public void CloseAll()
        {
            CancelAllPendingShows();
            for (var i = _stack.Count - 1; i >= 0; i--)
            {
                CloseWindow(_stack[i]);
            }
        }

        /// <summary>获取已打开的窗口实例。</summary>
        /// <typeparam name="TWindow">窗口类型。</typeparam>
        /// <returns>已打开则返回实例，否则返回 null。</returns>
        public TWindow Get<TWindow>() where TWindow : UIWindow
        {
            return _opened.TryGetValue(typeof(TWindow), out var window) ? (TWindow)window : null;
        }

        /// <summary>指定类型窗口是否已打开。</summary>
        /// <typeparam name="TWindow">窗口类型。</typeparam>
        /// <returns>已打开返回 true。</returns>
        public bool IsOpen<TWindow>() where TWindow : UIWindow
        {
            return _opened.ContainsKey(typeof(TWindow));
        }

        /// <summary>指定类型窗口是否已缓存（<see cref="UIReleasePolicy.Cached"/> / <see cref="UIReleasePolicy.HideAndDelayUnload"/> 等待复用或延迟卸载中）。</summary>
        /// <typeparam name="TWindow">窗口类型。</typeparam>
        /// <returns>已缓存返回 true。</returns>
        public bool IsCached<TWindow>() where TWindow : UIWindow
        {
            return _cached.ContainsKey(typeof(TWindow));
        }

        /// <summary>关闭并销毁全部窗口（含缓存），释放 UI 根节点。</summary>
        public void Shutdown()
        {
            CancelAllPendingShows();
            CancelAllDelayedUnloads();

            var destroyed = new HashSet<UIWindow>();
            foreach (var window in _opened.Values)
            {
                if (destroyed.Add(window))
                {
                    DestroyWindow(window, force: true);
                }
            }

            foreach (var window in _cached.Values)
            {
                if (destroyed.Add(window))
                {
                    DestroyWindow(window, force: true);
                }
            }

            _stack.Clear();
            _opened.Clear();
            _cached.Clear();

            foreach (var pair in _layerRoots)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value.gameObject);
                }
            }

            _layerRoots.Clear();

            if (_eventSystemRoot != null)
            {
                Destroy(_eventSystemRoot.gameObject);
                _eventSystemRoot = null;
            }

            if (_uiRoot != null)
            {
                Destroy(_uiRoot.gameObject);
                _uiRoot = null;
            }

            _initialized = false;
            GameLog.Info(LogCategories.UI, LogStyle.Muted("shut down"));
        }

        void Update()
        {
            if (!_initialized)
            {
                return;
            }

            var deltaTime = Time.deltaTime;
            for (var i = 0; i < _stack.Count; i++)
            {
                var window = _stack[i];
                if (window != null && window.IsCreated && window.GameObject != null && window.GameObject.activeSelf)
                {
                    window.OnUpdate(deltaTime);
                }
            }
        }

        IEnumerator ShowAsyncCoroutine<TWindow>(UIShowHandle operation, object userData, Action<TWindow> onComplete)
            where TWindow : UIWindow, new()
        {
            var windowType = typeof(TWindow);
            var metadata = ResolveMetadata(windowType);
            try
            {
                var loadRequest = ResourceManager.Instance.LoadAssetScheduled<GameObject>(
                    metadata.Location,
                    loaded => operation.SetAssetHandle(loaded),
                    priority: 10);
                operation.SetRequest(loadRequest);
                yield return loadRequest;

                if (operation.IsCancelled)
                {
                    yield break;
                }

                var handle = loadRequest.AssetHandle;
                if (!handle.IsValid || !handle.Succeeded)
                {
                    var error = string.IsNullOrEmpty(handle.Error) ? loadRequest.Error : handle.Error;
                    GameLog.Error(LogCategories.UI,
                        $"Load window failed: {LogStyle.Name(windowType.Name)} location={LogStyle.Value(metadata.Location)} error={error}");
                    operation.MarkFailed();
                    onComplete?.Invoke(null);
                    yield break;
                }

                operation.SetAssetHandle(handle);

                GameObject instance = null;
                var instantiateRequest = ResourceManager.Instance.InstantiateScheduled(
                    handle,
                    _layerRoots[metadata.Layer],
                    go =>
                    {
                        instance = go;
                        operation.SetInstance(go);
                    },
                    priority: 10);
                operation.SetRequest(instantiateRequest);
                yield return instantiateRequest;

                if (operation.IsCancelled)
                {
                    yield break;
                }

                if (instance == null)
                {
                    instance = instantiateRequest.Instance;
                }

                if (instance == null)
                {
                    GameLog.Error(LogCategories.UI,
                        $"Instantiate window failed: {LogStyle.Name(windowType.Name)} location={LogStyle.Value(metadata.Location)}");
                    operation.MarkFailed();
                    onComplete?.Invoke(null);
                    yield break;
                }

                var window = BindWindowInstance<TWindow>(instance, metadata, userData);
                window.AssetHandle = handle;
                operation.MarkBound();
                onComplete?.Invoke(window);
            }
            finally
            {
                operation.AbortIfStillIncomplete();
            }
        }

        bool CancelPendingShow(Type windowType)
        {
            if (windowType == null || !_pendingShows.TryGetValue(windowType, out var pending))
            {
                return false;
            }

            pending.Cancel();
            return true;
        }

        void CancelAllPendingShows()
        {
            if (_pendingShows.Count == 0)
            {
                return;
            }

            var pending = new List<UIShowHandle>(_pendingShows.Values);
            for (var i = 0; i < pending.Count; i++)
            {
                pending[i].Cancel();
            }
        }

        void DetachPendingShow(UIShowHandle operation)
        {
            if (operation?.WindowType == null)
            {
                return;
            }

            if (_pendingShows.TryGetValue(operation.WindowType, out var current) && current == operation)
            {
                _pendingShows.Remove(operation.WindowType);
            }
        }

        TWindow CreateWindowInstance<TWindow>(
            ResourceAssetHandle handle,
            WindowMetadata metadata,
            object userData) where TWindow : UIWindow, new()
        {
            var layerRoot = _layerRoots[metadata.Layer];
            var instance = handle.InstantiateSync(layerRoot);
            if (instance == null)
            {
                throw new InvalidOperationException(
                    $"[UI] Instantiate window failed: {typeof(TWindow).Name}, location={metadata.Location}");
            }

            return BindWindowInstance<TWindow>(instance, metadata, userData);
        }

        TWindow BindWindowInstance<TWindow>(
            GameObject instance,
            WindowMetadata metadata,
            object userData) where TWindow : UIWindow, new()
        {
            var layerRoot = _layerRoots[metadata.Layer];
            var sortingOrder = (int)metadata.Layer * LayerOrderStep + _stack.Count * WindowOrderStep;
            var window = new TWindow();
            window.ReleasePolicy = metadata.ReleasePolicy;
            window.DelayUnloadSeconds = metadata.DelayUnloadSeconds;
            window.InternalCreate(this, instance, null, userData);
            window.SetupWindow(layerRoot, metadata.Layer, metadata.FullScreen, sortingOrder);

            RegisterWindow(window);
            return window;
        }

        bool TryReviveCached<TWindow>(Type windowType, object userData, out TWindow window)
            where TWindow : UIWindow, new()
        {
            if (!_cached.TryGetValue(windowType, out var cached))
            {
                window = null;
                return false;
            }

            _cached.Remove(windowType);
            cached.UserData = userData;
            cached.SetVisible(true);
            RegisterWindow(cached);
            cached.OnRefresh();
            window = (TWindow)cached;
            GameLog.Info(LogCategories.UI,
                $"Revive cached {LogStyle.Name(windowType.Name)} policy={LogStyle.Value(cached.ReleasePolicy)}");
            return true;
        }

        void CloseWindow(UIWindow window)
        {
            if (window == null)
            {
                return;
            }

            switch (window.ReleasePolicy)
            {
                case UIReleasePolicy.HideOnly:
                    HideWindow(window);
                    break;
                case UIReleasePolicy.Cached:
                    CacheWindow(window);
                    break;
                case UIReleasePolicy.HideAndDelayUnload:
                    ScheduleDelayedUnload(window);
                    break;
                default:
                    DestroyWindow(window, force: true);
                    break;
            }
        }

        void HideWindow(UIWindow window)
        {
            var windowType = window.GetType();
            CancelDelayedUnload(windowType);
            _stack.Remove(window);
            window.SetVisible(false);
            GameLog.Info(LogCategories.UI,
                $"Hide {LogStyle.Name(windowType.Name)} policy={LogStyle.Value(window.ReleasePolicy)}");
            RefreshVisibility();
        }

        void CacheWindow(UIWindow window)
        {
            var windowType = window.GetType();
            CancelDelayedUnload(windowType);
            _stack.Remove(window);
            _opened.Remove(windowType);
            window.SetVisible(false);
            _cached[windowType] = window;
            GameLog.Info(LogCategories.UI,
                $"Close {LogStyle.Name(windowType.Name)} cached policy={LogStyle.Value(window.ReleasePolicy)}");
            RefreshVisibility();
        }

        void ScheduleDelayedUnload(UIWindow window)
        {
            CacheWindow(window);
            var windowType = window.GetType();
            CancelDelayedUnload(windowType);
            var handle = GameCoroutine.StartGlobal(DelayedUnloadCoroutine(windowType, window.DelayUnloadSeconds));
            _delayUnloadTimers[windowType] = handle;
        }

        IEnumerator DelayedUnloadCoroutine(Type windowType, float delaySeconds)
        {
            yield return new WaitForSeconds(delaySeconds);
            _delayUnloadTimers.Remove(windowType);
            if (!_cached.TryGetValue(windowType, out var window))
            {
                yield break;
            }

            _cached.Remove(windowType);
            DestroyWindow(window, force: true);
            GameLog.Info(LogCategories.UI,
                $"Delay unload {LogStyle.Name(windowType.Name)} after {LogStyle.Value(delaySeconds)}s");
        }

        void CancelDelayedUnload(Type windowType)
        {
            if (windowType == null || !_delayUnloadTimers.TryGetValue(windowType, out var handle))
            {
                return;
            }

            handle.Stop();
            _delayUnloadTimers.Remove(windowType);
        }

        void CancelAllDelayedUnloads()
        {
            if (_delayUnloadTimers.Count == 0)
            {
                return;
            }

            var timers = new List<ICoroutineHandle>(_delayUnloadTimers.Values);
            for (var i = 0; i < timers.Count; i++)
            {
                timers[i].Stop();
            }

            _delayUnloadTimers.Clear();
        }

        void DestroyWindow(UIWindow window, bool force)
        {
            if (window == null)
            {
                return;
            }

            if (!force && window.ReleasePolicy != UIReleasePolicy.DestroyImmediate)
            {
                CloseWindow(window);
                return;
            }

            var windowType = window.GetType();
            CancelDelayedUnload(windowType);
            _opened.Remove(windowType);
            _stack.Remove(window);
            _cached.Remove(windowType);

            var gameObject = window.GameObject;
            var assetHandle = window.AssetHandle;
            window.InternalDestroy();

            if (gameObject != null)
            {
                Destroy(gameObject);
            }

            if (assetHandle.IsValid)
            {
                assetHandle.Dispose();
            }

            GameLog.Info(LogCategories.UI, $"Destroy {LogStyle.Name(windowType.Name)}");
            RefreshVisibility();
        }

        void RegisterWindow(UIWindow window)
        {
            var windowType = window.GetType();
            _opened[windowType] = window;
            BringToTop(window);
            RefreshVisibility();
            GameLog.Info(LogCategories.UI, $"Open {LogStyle.Name(windowType.Name)} layer={LogStyle.Value(window.Layer)}");
        }

        void BringToTop(UIWindow window)
        {
            _stack.Remove(window);
            var insertIndex = _stack.Count;
            for (var i = _stack.Count - 1; i >= 0; i--)
            {
                if ((int)_stack[i].Layer <= (int)window.Layer)
                {
                    insertIndex = i + 1;
                    break;
                }

                insertIndex = i;
            }

            _stack.Insert(insertIndex, window);
            RecalculateSortingOrders();
        }

        void RecalculateSortingOrders()
        {
            var layerCounters = new Dictionary<UILayer, int>();
            for (var i = 0; i < _stack.Count; i++)
            {
                var window = _stack[i];
                if (!layerCounters.TryGetValue(window.Layer, out var counter))
                {
                    counter = 0;
                }

                var sortingOrder = (int)window.Layer * LayerOrderStep + counter * WindowOrderStep;
                window.SortingOrder = sortingOrder;

                var canvas = window.GameObject != null ? window.GameObject.GetComponent<Canvas>() : null;
                if (canvas != null)
                {
                    canvas.sortingOrder = sortingOrder;
                }

                layerCounters[window.Layer] = counter + 1;
            }
        }

        void RefreshVisibility()
        {
            var blockBelow = false;
            for (var i = _stack.Count - 1; i >= 0; i--)
            {
                var window = _stack[i];
                var visible = !blockBelow;
                window.SetVisible(visible);

                if (visible && window.FullScreen)
                {
                    blockBelow = true;
                }
            }
        }

        void EnsureLayer(UILayer layer)
        {
            if (_layerRoots.ContainsKey(layer))
            {
                return;
            }

            var layerGo = new GameObject(layer.ToString(), typeof(RectTransform));
            var rect = layerGo.GetComponent<RectTransform>();
            rect.SetParent(_uiRoot, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            var canvas = layerGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = (int)layer * LayerOrderStep;

            var scaler = layerGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            layerGo.AddComponent<GraphicRaycaster>();
            _layerRoots[layer] = rect;
        }

        void EnsureEventSystem()
        {
            if (_eventSystemRoot != null)
            {
                return;
            }

            var existing = transform.Find("EventSystem");
            if (existing != null)
            {
                _eventSystemRoot = existing;
                return;
            }

            var eventSystemGo = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            eventSystemGo.transform.SetParent(transform, false);
            _eventSystemRoot = eventSystemGo.transform;
        }

        void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }

        static WindowMetadata ResolveMetadata(Type windowType)
        {
            var attribute = (UIWindowAttribute)Attribute.GetCustomAttribute(windowType, typeof(UIWindowAttribute));
            return new WindowMetadata(
                attribute?.Layer ?? UILayer.UI,
                attribute?.FullScreen ?? false,
                string.IsNullOrEmpty(attribute?.Location) ? UIPaths.Window(windowType) : attribute.Location,
                attribute?.ReleasePolicy ?? UIReleasePolicy.DestroyImmediate,
                attribute?.DelayUnloadSeconds ?? 30f);
        }

        readonly struct WindowMetadata
        {
            public WindowMetadata(
                UILayer layer,
                bool fullScreen,
                string location,
                UIReleasePolicy releasePolicy,
                float delayUnloadSeconds)
            {
                Layer = layer;
                FullScreen = fullScreen;
                Location = location;
                ReleasePolicy = releasePolicy;
                DelayUnloadSeconds = delayUnloadSeconds;
            }

            public UILayer Layer { get; }
            public bool FullScreen { get; }
            public string Location { get; }
            public UIReleasePolicy ReleasePolicy { get; }
            public float DelayUnloadSeconds { get; }
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            Shutdown();
            base.OnDestroy();
        }
    }
}
