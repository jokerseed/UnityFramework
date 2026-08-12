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

        /// <summary>同步打开窗口；若已打开则仅刷新。</summary>
        /// <typeparam name="TWindow">窗口类型，须有无参构造。</typeparam>
        /// <param name="userData">传入窗口的用户数据；可为 null。</param>
        /// <returns>打开的窗口实例。</returns>
        /// <exception cref="InvalidOperationException">资源加载或实例化失败。</exception>
        public TWindow Show<TWindow>(object userData = null) where TWindow : UIWindow, new()
        {
            EnsureInitialized();

            var windowType = typeof(TWindow);
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

        /// <summary>异步打开窗口。</summary>
        /// <typeparam name="TWindow">窗口类型，须有无参构造。</typeparam>
        /// <param name="userData">传入窗口的用户数据；可为 null。</param>
        /// <param name="onComplete">完成回调；参数为窗口实例或失败时的 null。</param>
        /// <returns>协程句柄。</returns>
        public ICoroutineHandle ShowAsync<TWindow>(object userData = null, Action<TWindow> onComplete = null)
            where TWindow : UIWindow, new()
        {
            return GameCoroutine.StartGlobal(ShowAsyncCoroutine(userData, onComplete));
        }

        /// <summary>关闭指定类型窗口。</summary>
        /// <typeparam name="TWindow">窗口类型。</typeparam>
        /// <returns>成功关闭返回 true；窗口未打开返回 false。</returns>
        public bool Close<TWindow>() where TWindow : UIWindow
        {
            return Close(typeof(TWindow));
        }

        /// <summary>关闭指定类型窗口。</summary>
        /// <param name="windowType">窗口类型，不可为 null。</param>
        /// <returns>成功关闭返回 true；窗口未打开返回 false。</returns>
        public bool Close(Type windowType)
        {
            if (windowType == null || !_opened.TryGetValue(windowType, out var window))
            {
                return false;
            }

            DestroyWindow(window);
            return true;
        }

        /// <summary>关闭全部已打开窗口。</summary>
        public void CloseAll()
        {
            for (var i = _stack.Count - 1; i >= 0; i--)
            {
                DestroyWindow(_stack[i]);
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

        /// <summary>关闭并销毁全部窗口，释放 UI 根节点。</summary>
        public void Shutdown()
        {
            CloseAll();

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

        IEnumerator ShowAsyncCoroutine<TWindow>(object userData, Action<TWindow> onComplete)
            where TWindow : UIWindow, new()
        {
            EnsureInitialized();

            var windowType = typeof(TWindow);
            if (_opened.TryGetValue(windowType, out var existing))
            {
                existing.UserData = userData;
                existing.OnRefresh();
                BringToTop(existing);
                RefreshVisibility();
                onComplete?.Invoke((TWindow)existing);
                yield break;
            }

            var metadata = ResolveMetadata(windowType);
            ResourceAssetHandle handle = default;
            yield return ResourceManager.Instance.LoadAssetAsync<GameObject>(metadata.Location, loaded => handle = loaded);

            if (!handle.IsValid || !handle.Succeeded)
            {
                GameLog.Error(LogCategories.UI,
                    $"Load window failed: {LogStyle.Name(windowType.Name)} location={LogStyle.Value(metadata.Location)} error={handle.Error}");
                handle.Dispose();
                onComplete?.Invoke(null);
                yield break;
            }

            var window = CreateWindowInstance<TWindow>(handle, metadata, userData);
            window.AssetHandle = handle;
            onComplete?.Invoke(window);
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

            var sortingOrder = (int)metadata.Layer * LayerOrderStep + _stack.Count * WindowOrderStep;
            var window = new TWindow();
            window.InternalCreate(this, instance, null, userData);
            window.SetupWindow(layerRoot, metadata.Layer, metadata.FullScreen, sortingOrder);

            RegisterWindow(window);
            return window;
        }

        void RegisterWindow(UIWindow window)
        {
            var windowType = window.GetType();
            _opened[windowType] = window;
            BringToTop(window);
            RefreshVisibility();
            GameLog.Info(LogCategories.UI, $"Open {LogStyle.Name(windowType.Name)} layer={LogStyle.Value(window.Layer)}");
        }

        void DestroyWindow(UIWindow window)
        {
            if (window == null)
            {
                return;
            }

            var windowType = window.GetType();
            _opened.Remove(windowType);
            _stack.Remove(window);

            var gameObject = window.GameObject;
            var assetHandle = window.AssetHandle;
            window.InternalDestroy();

            if (gameObject != null)
            {
                Destroy(gameObject);
            }

            assetHandle.Dispose();
            GameLog.Info(LogCategories.UI, $"Close {LogStyle.Name(windowType.Name)}");
            RefreshVisibility();
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
                string.IsNullOrEmpty(attribute?.Location) ? UIPaths.Window(windowType) : attribute.Location);
        }

        readonly struct WindowMetadata
        {
            public WindowMetadata(UILayer layer, bool fullScreen, string location)
            {
                Layer = layer;
                FullScreen = fullScreen;
                Location = location;
            }

            public UILayer Layer { get; }
            public bool FullScreen { get; }
            public string Location { get; }
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            Shutdown();
            base.OnDestroy();
        }
    }
}
