using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Core;
using Framework.Logging;
using UnityEngine;
using UnityEngine.SceneManagement;
using YooAsset;

namespace Framework.Res
{
    /// <summary>
    /// 运行时资源包管理器，封装 YooAsset 的初始化与通用加载/释放流程。
    /// 是运行时 Asset、Scene、bytes 的合法加载入口；Luban 配置表见 <see cref="Framework.Config.ConfigManager"/>。
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class ResourceManager : PersistentSingleton<ResourceManager>
    {
        readonly Dictionary<string, ResourceAssetHandle> _syncCache = new Dictionary<string, ResourceAssetHandle>();
        readonly List<ResourceSceneHandle> _additiveHandles = new List<ResourceSceneHandle>(4);

        ResourceSceneHandle _mainSceneHandle;

        [SerializeField] ResourceInitOptions _initOptions = new ResourceInitOptions();
        [SerializeField] ResourceSchedulerOptions _schedulerOptions = new ResourceSchedulerOptions();

        ResourcePackage _package;
        ResourceInitOptions _options;
        ResourceScheduler _scheduler;
        bool _initialized;
        bool _stopped;

        /// <summary>Inspector 中配置的资源初始化选项；未赋值时返回默认配置。</summary>
        public ResourceInitOptions InitOptions
        {
            get
            {
                if (_initOptions == null)
                {
                    _initOptions = new ResourceInitOptions();
                }

                return _initOptions;
            }
        }

        /// <summary>是否已完成初始化。</summary>
        public bool IsInitialized => _initialized;

        /// <summary>当前使用的资源包名称。</summary>
        public string PackageName => _options != null ? _options.PackageName : ResourceInitOptions.DefaultPackageName;

        /// <summary>分帧调度预算；未赋值时使用默认配置。</summary>
        public ResourceSchedulerOptions SchedulerOptions
        {
            get
            {
                if (_schedulerOptions == null)
                {
                    _schedulerOptions = new ResourceSchedulerOptions();
                }

                return _schedulerOptions;
            }
        }

        /// <summary>等待发起的异步加载数量（同地址合并后的组数）。</summary>
        public int PendingLoadCount => _scheduler != null ? _scheduler.PendingLoadCount : 0;

        /// <summary>进行中的异步加载数量（同地址合并后的组数）。</summary>
        public int InFlightLoadCount => _scheduler != null ? _scheduler.InFlightCount : 0;

        /// <summary>等待实例化的数量。</summary>
        public int PendingInstantiateCount => _scheduler != null ? _scheduler.PendingInstantiateCount : 0;

        /// <summary>Unity 当前激活场景。</summary>
        public Scene ActiveScene => SceneManager.GetActiveScene();

        /// <summary>最近一次 <see cref="LoadMainSceneAsync"/> 成功加载的主场景；尚未加载或已切换时为无效 Scene。</summary>
        public Scene CurrentMainScene => _mainSceneHandle.IsValid ? _mainSceneHandle.Scene : default;

        /// <summary>当前登记的主场景句柄；无效表示尚未通过 <see cref="LoadMainSceneAsync"/> 加载。</summary>
        public ResourceSceneHandle CurrentMainSceneHandle => _mainSceneHandle;

        /// <summary>当前登记的 Additive 场景句柄（只读副本）。</summary>
        public IReadOnlyList<ResourceSceneHandle> AdditiveSceneHandles => _additiveHandles;

        /// <summary>异步初始化资源包（请求版本号 + 加载 Manifest）。</summary>
        /// <param name="options">初始化选项；不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="options"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">包初始化、版本请求或 Manifest 加载任一步骤失败。</exception>
        public IEnumerator InitializeAsync(ResourceInitOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _stopped = false;
            _options = options;

            if (!YooAssets.IsInitialized)
            {
                YooAssets.Initialize(new YooAssetLogAdapter());
            }

            if (!YooAssets.TryGetPackage(options.PackageName, out _package))
            {
                _package = YooAssets.CreatePackage(options.PackageName);
            }

            var initOperation = CreateInitializeOperation(options);
            yield return initOperation;
            if (initOperation.Status != EOperationStatus.Succeeded)
            {
                throw new InvalidOperationException($"[Resource] Initialize package failed: {initOperation.Error}");
            }

            var versionOperation = _package.RequestPackageVersionAsync();
            yield return versionOperation;
            if (versionOperation.Status != EOperationStatus.Succeeded)
            {
                throw new InvalidOperationException($"[Resource] Request package version failed: {versionOperation.Error}");
            }

            var manifestOperation = _package.LoadPackageManifestAsync(
                new LoadPackageManifestOptions(versionOperation.PackageVersion, options.ManifestLoadTimeoutSeconds));
            yield return manifestOperation;
            if (manifestOperation.Status != EOperationStatus.Succeeded)
            {
                throw new InvalidOperationException($"[Resource] Load package manifest failed: {manifestOperation.Error}");
            }

            _initialized = true;
            _scheduler?.Shutdown();
            _scheduler = new ResourceScheduler(SchedulerOptions, StartScheduledLoad, StartScheduledUnload);
            GameLog.Info(LogCategories.Resource, $"Package {LogStyle.Ok("ready")}: {LogStyle.Name(options.PackageName)}  version={LogStyle.Value(versionOperation.PackageVersion)}");
        }

        void Update()
        {
            if (_initialized && !_stopped)
            {
                _scheduler?.Tick();
            }
        }

        /// <summary>同步加载指定寻址的资源，返回封装句柄。</summary>
        /// <typeparam name="T">资源类型，须继承 <see cref="UnityEngine.Object"/>。</typeparam>
        /// <param name="location">YooAsset 寻址字符串，建议通过 <see cref="ResourceAddresses"/> 生成。</param>
        /// <returns>资源句柄；使用完毕须调用 <see cref="ResourceAssetHandle.Dispose"/> 或通过 using 释放。</returns>
        /// <exception cref="InvalidOperationException">管理器尚未初始化。</exception>
        public ResourceAssetHandle LoadAssetSync<T>(string location) where T : UnityEngine.Object
        {
            EnsureInitialized();
            var handle = _package.LoadAssetSync<T>(location);
            return new ResourceAssetHandle(handle);
        }

        /// <summary>
        /// 异步加载指定寻址的资源，完成后通过回调返回句柄。
        /// 内部进入 <see cref="ResourceScheduler"/> 分帧队列，不在调用当帧立即发起 YooAsset 加载。
        /// 同一 location + 资源类型若已在排队或加载中，会挂到同一路 InFlight，完成后每个调用方仍获得可独立 Dispose 的句柄。
        /// </summary>
        /// <typeparam name="T">资源类型，须继承 <see cref="UnityEngine.Object"/>。</typeparam>
        /// <param name="location">YooAsset 寻址字符串。</param>
        /// <param name="onComplete">加载完成后的回调；参数为已包装的资源句柄；可为 null。失败或取消时句柄无效。</param>
        /// <param name="priority">优先级，数值越大越先处理；默认 0。</param>
        /// <exception cref="InvalidOperationException">管理器尚未初始化。</exception>
        public IEnumerator LoadAssetAsync<T>(
            string location,
            Action<ResourceAssetHandle> onComplete,
            int priority = 0) where T : UnityEngine.Object
        {
            var request = LoadAssetScheduled<T>(location, onComplete, priority);
            yield return request;
        }

        /// <summary>
        /// 将异步加载入队，立即返回请求句柄。完成时机由调度器按预算决定。
        /// 同一 location + 资源类型会合并为一次 YooAsset 加载。
        /// </summary>
        /// <typeparam name="T">资源类型，须继承 <see cref="UnityEngine.Object"/>。</typeparam>
        /// <param name="location">YooAsset 寻址字符串。</param>
        /// <param name="onComplete">加载完成后的回调；可为 null。</param>
        /// <param name="priority">优先级，数值越大越先处理；默认 0。</param>
        /// <returns>可用于等待或取消的请求句柄。</returns>
        /// <exception cref="InvalidOperationException">管理器尚未初始化。</exception>
        public ResourceRequestHandle LoadAssetScheduled<T>(
            string location,
            Action<ResourceAssetHandle> onComplete = null,
            int priority = 0) where T : UnityEngine.Object
        {
            EnsureInitialized();
            return _scheduler.EnqueueLoad(location, typeof(T), onComplete, priority);
        }

        /// <summary>
        /// 将实例化入队；内部仍调用 <see cref="ResourceAssetHandle.InstantiateSync"/>，但按时间预算分帧执行。
        /// </summary>
        /// <param name="handle">已成功加载的资源句柄。</param>
        /// <param name="parent">父 Transform；为 null 时实例化到场景根。</param>
        /// <param name="onComplete">完成后的回调；失败时参数为 null。</param>
        /// <param name="worldPositionStays">是否保持世界坐标；仅在 <paramref name="parent"/> 非 null 时有效。</param>
        /// <param name="priority">优先级，数值越大越先处理；默认 0。</param>
        /// <returns>可用于等待或取消的请求句柄。</returns>
        /// <exception cref="InvalidOperationException">管理器尚未初始化。</exception>
        public ResourceRequestHandle InstantiateScheduled(
            ResourceAssetHandle handle,
            Transform parent = null,
            Action<GameObject> onComplete = null,
            bool worldPositionStays = false,
            int priority = 0)
        {
            EnsureInitialized();
            return _scheduler.EnqueueInstantiate(handle, parent, worldPositionStays, onComplete, priority);
        }

        /// <summary>
        /// 协程等待调度队列完成实例化。
        /// </summary>
        /// <param name="handle">已成功加载的资源句柄。</param>
        /// <param name="parent">父 Transform；为 null 时实例化到场景根。</param>
        /// <param name="onComplete">完成后的回调；失败时参数为 null。</param>
        /// <param name="worldPositionStays">是否保持世界坐标。</param>
        /// <param name="priority">优先级，数值越大越先处理；默认 0。</param>
        /// <returns>协程迭代器。</returns>
        public IEnumerator InstantiateAsync(
            ResourceAssetHandle handle,
            Transform parent = null,
            Action<GameObject> onComplete = null,
            bool worldPositionStays = false,
            int priority = 0)
        {
            var request = InstantiateScheduled(handle, parent, onComplete, worldPositionStays, priority);
            yield return request;
        }

        /// <summary>
        /// 请求卸载未使用资源。多次调用会合并为一次；默认等 Load / Instantiate 队列空闲后再执行。
        /// </summary>
        public void RequestUnloadUnusedAssets()
        {
            EnsureInitialized();
            _scheduler.RequestUnloadUnusedAssets();
        }

        /// <summary>异步加载指定寻址的场景，完成后通过回调返回句柄。</summary>
        /// <param name="location">YooAsset 场景寻址字符串，建议通过 <see cref="ResourceAddresses.Scene"/> 生成。</param>
        /// <param name="sceneMode">场景加载模式；默认 <see cref="LoadSceneMode.Single"/>。</param>
        /// <param name="onComplete">加载完成后的回调；参数为已包装的场景句柄；可为 null。</param>
        /// <exception cref="InvalidOperationException">管理器尚未初始化，或场景加载失败。</exception>
        public IEnumerator LoadSceneAsync(
            string location,
            LoadSceneMode sceneMode = LoadSceneMode.Single,
            Action<ResourceSceneHandle> onComplete = null)
        {
            EnsureInitialized();
            yield return LoadSceneInternal(location, sceneMode, onComplete);
        }

        /// <summary>
        /// 以 Single 模式加载主场景：释放旧主场景句柄，并 Dispose 全部已登记 Additive 句柄。
        /// Unity 会卸掉旧场景，此处只负责 YooAsset 引用计数。
        /// </summary>
        /// <param name="location">YooAsset 场景寻址字符串。</param>
        /// <param name="onComplete">加载完成后的回调；参数为新主场景句柄；可为 null。</param>
        /// <returns>协程迭代器。</returns>
        /// <exception cref="InvalidOperationException">管理器尚未初始化，或场景加载失败。</exception>
        public IEnumerator LoadMainSceneAsync(string location, Action<ResourceSceneHandle> onComplete = null)
        {
            EnsureInitialized();
            ReleaseMainSceneHandle();
            ReleaseAllAdditiveHandles();

            ResourceSceneHandle loaded = default;
            yield return LoadSceneInternal(location, LoadSceneMode.Single, handle => loaded = handle);
            _mainSceneHandle = loaded;
            onComplete?.Invoke(loaded);
        }

        /// <summary>以 Additive 模式加载场景，并将句柄登记到内部列表。</summary>
        /// <param name="location">YooAsset 场景寻址字符串。</param>
        /// <param name="onComplete">加载完成后的回调；参数为已登记的场景句柄；可为 null。</param>
        /// <returns>协程迭代器。</returns>
        /// <exception cref="InvalidOperationException">管理器尚未初始化，或场景加载失败。</exception>
        public IEnumerator LoadAdditiveSceneAsync(string location, Action<ResourceSceneHandle> onComplete = null)
        {
            EnsureInitialized();

            ResourceSceneHandle loaded = default;
            yield return LoadSceneInternal(location, LoadSceneMode.Additive, handle => loaded = handle);
            _additiveHandles.Add(loaded);
            onComplete?.Invoke(loaded);
        }

        /// <summary>
        /// 卸载并释放指定 Additive 场景句柄。须为 <see cref="LoadAdditiveSceneAsync"/> 返回的句柄。
        /// </summary>
        /// <param name="handle">要卸载的 Additive 场景句柄。</param>
        /// <returns>协程迭代器。</returns>
        /// <exception cref="InvalidOperationException">管理器尚未初始化。</exception>
        public IEnumerator UnloadAdditiveSceneAsync(ResourceSceneHandle handle)
        {
            EnsureInitialized();
            if (!handle.IsValid)
            {
                yield break;
            }

            RemoveAdditiveHandle(handle);
            yield return handle.UnloadAsync();
            handle.Dispose();
        }

        /// <summary>同步加载寻址资源的原始字节。</summary>
        /// <param name="location">YooAsset 寻址字符串。</param>
        /// <param name="cache">为 true 时缓存句柄，须配合 <see cref="ReleaseCache"/>。</param>
        /// <returns>资源字节内容。</returns>
        public byte[] LoadBytes(string location, bool cache = false)
        {
            EnsureInitialized();

            if (cache)
            {
                if (_syncCache.TryGetValue(location, out var cached) && cached.IsValid)
                {
                    var cachedAsset = cached.GetAsset<TextAsset>();
                    return cachedAsset != null ? cachedAsset.bytes : Array.Empty<byte>();
                }

                var cachedHandle = LoadAssetSync<TextAsset>(location);
                if (!cachedHandle.IsValid || !cachedHandle.Succeeded)
                {
                    cachedHandle.Dispose();
                    throw new InvalidOperationException(
                        $"[Resource] Load bytes failed: location={location}, error={cachedHandle.Error}");
                }

                _syncCache[location] = cachedHandle;
                var asset = cachedHandle.GetAsset<TextAsset>();
                return asset != null ? asset.bytes : Array.Empty<byte>();
            }

            using (var handle = LoadAssetSync<TextAsset>(location))
            {
                if (!handle.IsValid || !handle.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"[Resource] Load bytes failed: location={location}, error={handle.Error}");
                }

                var asset = handle.GetAsset<TextAsset>();
                return asset != null ? asset.bytes : Array.Empty<byte>();
            }
        }

        /// <summary>加载字节并通过回调反序列化为对象。</summary>
        /// <typeparam name="T">目标类型。</typeparam>
        /// <param name="location">YooAsset 寻址字符串。</param>
        /// <param name="deserialize">bytes → 对象的反序列化函数。</param>
        /// <param name="cache">为 true 时缓存底层资源句柄。</param>
        /// <returns>反序列化结果。</returns>
        public T LoadBinary<T>(string location, Func<byte[], T> deserialize, bool cache = false)
        {
            if (deserialize == null)
            {
                throw new ArgumentNullException(nameof(deserialize));
            }

            var bytes = LoadBytes(location, cache);
            if (bytes == null || bytes.Length == 0)
            {
                throw new InvalidOperationException($"[Resource] Empty bytes: location={location}");
            }

            return deserialize(bytes);
        }

        /// <summary>释放所有通过 <see cref="LoadBytes(string, bool)"/>（<c>cache=true</c>）缓存的资源句柄。</summary>
        public void ReleaseCache()
        {
            foreach (var pair in _syncCache)
            {
                pair.Value.Dispose();
            }

            _syncCache.Clear();
        }

        /// <summary>
        /// 释放缓存并销毁 YooAsset；由 <see cref="ResourceModule"/> Shutdown 或退出 Play 时调用。
        /// 可重复调用，仅首次生效。
        /// </summary>
        public void Shutdown()
        {
            if (_stopped)
            {
                return;
            }

            _stopped = true;
            _scheduler?.Shutdown();
            _scheduler = null;
            ReleaseCache();
            ReleaseMainSceneHandle();
            ReleaseAllAdditiveHandles();
            _initialized = false;
            _package = null;
            _options = null;

            if (YooAssets.IsInitialized)
            {
                YooAssets.Destroy();
            }

            GameLog.Info(LogCategories.Resource, $"Manager {LogStyle.Muted("shut down")}");
        }

        /// <summary>退出 Play / 应用时先于 YooAsset Driver 销毁资源系统，避免 abort Warning。</summary>
        protected override void OnApplicationQuit()
        {
            Shutdown();
            base.OnApplicationQuit();
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            Shutdown();
            base.OnDestroy();
        }

        InitializePackageOperation CreateInitializeOperation(ResourceInitOptions options)
        {
            switch (options.PlayMode)
            {
                case ResourcePlayMode.EditorSimulate:
                {
                    var buildResult = EditorSimulateBuildInvoker.Build(
                        options.PackageName,
                        (int)EBundleType.VirtualAssetBundle);
                    var simulateOptions = new EditorSimulateModeOptions();
                    simulateOptions.EditorFileSystemParameters =
                        FileSystemParameters.CreateDefaultEditorFileSystemParameters(buildResult.PackageRootDirectory);
                    return _package.InitializePackageAsync(simulateOptions);
                }
                case ResourcePlayMode.Offline:
                {
                    var offlineOptions = new OfflinePlayModeOptions();
                    offlineOptions.BuiltinFileSystemParameters =
                        FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
                    return _package.InitializePackageAsync(offlineOptions);
                }
                case ResourcePlayMode.Host:
                {
                    var fallback = string.IsNullOrEmpty(options.FallbackHostServerUrl)
                        ? options.HostServerUrl
                        : options.FallbackHostServerUrl;
                    var remoteService = new SimpleRemoteService(options.HostServerUrl, fallback);
                    var hostOptions = new HostPlayModeOptions();
                    hostOptions.BuiltinFileSystemParameters =
                        FileSystemParameters.CreateDefaultBuiltinFileSystemParameters();
                    hostOptions.CacheFileSystemParameters =
                        FileSystemParameters.CreateDefaultSandboxFileSystemParameters(remoteService);
                    return _package.InitializePackageAsync(hostOptions);
                }
                default:
                    throw new NotSupportedException($"[Resource] Unsupported play mode: {options.PlayMode}");
            }
        }

        AssetHandle StartScheduledLoad(string location, Type assetType)
        {
            return _package.LoadAssetAsync(location, assetType);
        }

        UnloadUnusedAssetsOperation StartScheduledUnload()
        {
            GameLog.Info(LogCategories.Resource, LogStyle.Muted("UnloadUnusedAssets started"));
            return _package.UnloadUnusedAssetsAsync();
        }

        void EnsureInitialized()
        {
            if (!_initialized || _package == null || _scheduler == null)
            {
                throw new InvalidOperationException("[Resource] ResourceManager is not initialized.");
            }
        }

        IEnumerator LoadSceneInternal(
            string location,
            LoadSceneMode sceneMode,
            Action<ResourceSceneHandle> onComplete)
        {
            var handle = _package.LoadSceneAsync(location, sceneMode);
            yield return handle;

            var wrapped = new ResourceSceneHandle(handle);
            if (!wrapped.IsValid || !wrapped.Succeeded)
            {
                wrapped.Dispose();
                throw new InvalidOperationException(
                    $"[Resource] Load scene failed: location={location}, mode={sceneMode}, error={wrapped.Error}");
            }

            GameLog.Info(LogCategories.Resource,
                $"Scene {LogStyle.Ok("loaded")}: {LogStyle.Name(wrapped.SceneName)}  mode={LogStyle.Value(sceneMode)}  location={LogStyle.Value(location)}");
            onComplete?.Invoke(wrapped);
        }

        void ReleaseMainSceneHandle()
        {
            if (!_mainSceneHandle.IsValid)
            {
                _mainSceneHandle = default;
                return;
            }

            _mainSceneHandle.Dispose();
            _mainSceneHandle = default;
        }

        void ReleaseAllAdditiveHandles()
        {
            for (var i = 0; i < _additiveHandles.Count; i++)
            {
                if (_additiveHandles[i].IsValid)
                {
                    _additiveHandles[i].Dispose();
                }
            }

            _additiveHandles.Clear();
        }

        void RemoveAdditiveHandle(ResourceSceneHandle handle)
        {
            for (var i = _additiveHandles.Count - 1; i >= 0; i--)
            {
                if (_additiveHandles[i].Equals(handle))
                {
                    _additiveHandles.RemoveAt(i);
                }
            }
        }

        sealed class SimpleRemoteService : IRemoteService
        {
            readonly string _defaultHostServer;
            readonly string _fallbackHostServer;

            public SimpleRemoteService(string defaultHostServer, string fallbackHostServer)
            {
                _defaultHostServer = defaultHostServer;
                _fallbackHostServer = fallbackHostServer;
            }

            public IReadOnlyList<string> GetRemoteUrls(string fileName)
            {
                return new[]
                {
                    $"{_defaultHostServer}/{fileName}",
                    $"{_fallbackHostServer}/{fileName}",
                };
            }
        }
    }
}
