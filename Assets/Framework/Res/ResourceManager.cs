using System;
using System.Collections;
using System.Collections.Generic;
using Framework.Core;
using Framework.Logging;
using UnityEngine;
using YooAsset;

namespace Framework.Res
{
    public sealed class ResourceManager : PersistentSingleton<ResourceManager>
    {
        readonly Dictionary<string, ResourceAssetHandle> _syncCache = new Dictionary<string, ResourceAssetHandle>();

        ResourcePackage _package;
        ResourceInitOptions _options;
        bool _initialized;

        public bool IsInitialized => _initialized;
        public string PackageName => _options != null ? _options.PackageName : ResourceInitOptions.DefaultPackageName;

        public IEnumerator InitializeAsync(ResourceInitOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _options = options;

            if (!YooAssets.IsInitialized)
            {
                YooAssets.Initialize();
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
            GameLog.Info(LogCategories.Resource, $"Package ready: {options.PackageName}, version={versionOperation.PackageVersion}");
        }

        public ResourceAssetHandle LoadAssetSync<T>(string location) where T : UnityEngine.Object
        {
            EnsureInitialized();
            var handle = _package.LoadAssetSync<T>(location);
            return new ResourceAssetHandle(handle);
        }

        public IEnumerator LoadAssetAsync<T>(string location, Action<ResourceAssetHandle> onComplete) where T : UnityEngine.Object
        {
            EnsureInitialized();

            var handle = _package.LoadAssetAsync<T>(location);
            yield return handle;

            var wrapped = new ResourceAssetHandle(handle);
            onComplete?.Invoke(wrapped);
        }

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

        public void ReleaseCache()
        {
            foreach (var pair in _syncCache)
            {
                pair.Value.Dispose();
            }

            _syncCache.Clear();
        }

        public void Shutdown()
        {
            ReleaseCache();
            _initialized = false;
            _package = null;
            _options = null;
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
