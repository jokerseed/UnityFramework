using System;
using System.Collections;
using System.Collections.Generic;
using cfg;
using Framework.Core;
using Framework.Logging;
using Luban;
using UnityEngine;
using YooAsset;

namespace Framework.Res
{
    /// <summary>
    /// 运行时资源包管理器，封装 YooAsset 的初始化与加载流程。
    /// 是运行时唯一合法的 Asset / 配置字节加载入口；通过 <see cref="ResourceModule"/> 驱动初始化。
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class ResourceManager : PersistentSingleton<ResourceManager>
    {
        readonly Dictionary<string, ResourceAssetHandle> _syncCache = new Dictionary<string, ResourceAssetHandle>();

        [SerializeField] ResourceInitOptions _initOptions = new ResourceInitOptions();

        ResourcePackage _package;
        ResourceInitOptions _options;
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
            GameLog.Info(LogCategories.Resource, $"Package {LogStyle.Ok("ready")}: {LogStyle.Name(options.PackageName)}  version={LogStyle.Value(versionOperation.PackageVersion)}");
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

        /// <summary>异步加载指定寻址的资源，完成后通过回调返回句柄。</summary>
        /// <typeparam name="T">资源类型，须继承 <see cref="UnityEngine.Object"/>。</typeparam>
        /// <param name="location">YooAsset 寻址字符串。</param>
        /// <param name="onComplete">加载完成后的回调；参数为已包装的资源句柄；可为 null。</param>
        /// <exception cref="InvalidOperationException">管理器尚未初始化。</exception>
        public IEnumerator LoadAssetAsync<T>(string location, Action<ResourceAssetHandle> onComplete) where T : UnityEngine.Object
        {
            EnsureInitialized();

            var handle = _package.LoadAssetAsync<T>(location);
            yield return handle;

            var wrapped = new ResourceAssetHandle(handle);
            onComplete?.Invoke(wrapped);
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

        /// <summary>加载 Luban 全量配置表（运行时推荐，一次调用完成）。</summary>
        /// <param name="cacheTableAssets">是否缓存各表 TextAsset 句柄。</param>
        /// <returns>Luban <see cref="Tables"/>。</returns>
        public Tables LoadLubanTables(bool cacheTableAssets = true)
        {
            EnsureInitialized();
            return new Tables(file =>
            {
                var bytes = cacheTableAssets ? LoadConfigBytesCached(file) : LoadConfigBytes(file);
                return new ByteBuf(bytes);
            });
        }

        /// <summary>同步加载配置表原始字节（不缓存），加载完成后立即释放句柄。</summary>
        /// <param name="tableName">Luban 表名（如 <c>tbability</c>），内部自动生成寻址路径。</param>
        /// <returns>配置表原始字节；若 Asset 为 null 则返回空数组。</returns>
        /// <exception cref="InvalidOperationException">加载失败时。</exception>
        public byte[] LoadConfigBytes(string tableName)
        {
            var location = ResourceAddresses.ConfigTable(tableName);
            using (var handle = LoadAssetSync<TextAsset>(location))
            {
                if (!handle.IsValid || !handle.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"[Resource] Load config failed: {tableName}, location={location}, error={handle.Error}");
                }

                var asset = handle.GetAsset<TextAsset>();
                return asset != null ? asset.bytes : Array.Empty<byte>();
            }
        }

        /// <summary>同步加载配置表字节并缓存句柄；重复调用不重复加载。须调用 <see cref="ReleaseCache"/> 释放。</summary>
        /// <param name="tableName">Luban 表名（如 <c>tbability</c>）。</param>
        /// <returns>配置表原始字节；若 Asset 为 null 则返回空数组。</returns>
        /// <exception cref="InvalidOperationException">加载失败时。</exception>
        public byte[] LoadConfigBytesCached(string tableName)
        {
            var location = ResourceAddresses.ConfigTable(tableName);
            if (_syncCache.TryGetValue(location, out var cached) && cached.IsValid)
            {
                var cachedAsset = cached.GetAsset<TextAsset>();
                return cachedAsset != null ? cachedAsset.bytes : Array.Empty<byte>();
            }

            var handle = LoadAssetSync<TextAsset>(location);
            if (!handle.IsValid || !handle.Succeeded)
            {
                handle.Dispose();
                throw new InvalidOperationException(
                    $"[Resource] Load config failed: {tableName}, location={location}, error={handle.Error}");
            }

            _syncCache[location] = handle;
            var asset = handle.GetAsset<TextAsset>();
            return asset != null ? asset.bytes : Array.Empty<byte>();
        }

        /// <summary>释放所有通过 <see cref="LoadConfigBytesCached"/> 缓存的资源句柄。</summary>
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
            ReleaseCache();
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

        void EnsureInitialized()
        {
            if (!_initialized || _package == null)
            {
                throw new InvalidOperationException("[Resource] ResourceManager is not initialized.");
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
