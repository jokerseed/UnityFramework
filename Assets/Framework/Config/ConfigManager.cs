using System;
using System.Collections.Generic;
using cfg;
using Framework.Core;
using Framework.Logging;
using Framework.Res;
using Luban;
using UnityEngine;

namespace Framework.Config
{
    /// <summary>
    /// 配置管理器：Luban <see cref="CfgTables"/> 的按需加载与缓存入口。
    /// 不在模块初始化时读表；业务首次需要时调用 <see cref="LoadTables"/>。
    /// 配置表 Bundle 的加载/释放逻辑归属本类，底层仅调用 <see cref="ResourceManager"/> 通用 API。
    /// </summary>
    [DefaultExecutionOrder(-450)]
    public sealed class ConfigManager : PersistentSingleton<ConfigManager>
    {
        readonly Dictionary<string, ResourceAssetHandle> _tableAssetCache = new Dictionary<string, ResourceAssetHandle>();

        CfgTables _tables;

        /// <summary>是否已加载并缓存运行时 <see cref="CfgTables"/>。</summary>
        public bool IsLoaded => _tables != null;

        /// <summary>
        /// 当前缓存的 <see cref="CfgTables"/>；尚未加载时为 null。
        /// 需要保证已加载时请用 <see cref="LoadTables"/>。
        /// </summary>
        public CfgTables Tables => _tables;

        /// <summary>
        /// 按需加载 Luban 全表并缓存；已加载则直接返回缓存。
        /// </summary>
        /// <param name="cacheTableAssets">是否缓存各表 TextAsset 句柄（由本类 table asset 缓存管理）。</param>
        /// <returns>Luban <see cref="CfgTables"/>。</returns>
        /// <exception cref="InvalidOperationException">ResourceManager 未就绪或加载失败。</exception>
        public CfgTables LoadTables(bool cacheTableAssets = true)
        {
            if (_tables != null)
            {
                return _tables;
            }

            _tables = LoadLubanTables(cacheTableAssets);
            GameLog.Info(LogCategories.Config, $"CfgTables {LogStyle.Ok("loaded")} (cached)");
            return _tables;
        }

        /// <summary>
        /// 从 Bundle 加载 Luban 全表；不写入 <see cref="Tables"/> 缓存。
        /// 需要 <see cref="CfgTables"/> 对象但不走 <see cref="LoadTables"/> 单例缓存时使用。
        /// </summary>
        /// <param name="cacheTableAssets">是否缓存各表 TextAsset 句柄。</param>
        /// <returns>Luban <see cref="CfgTables"/>。</returns>
        /// <exception cref="InvalidOperationException">ResourceManager 未就绪或加载失败。</exception>
        public CfgTables LoadLubanTables(bool cacheTableAssets = true)
        {
            EnsureResourceReady();
            return new CfgTables(file =>
            {
                var bytes = LoadConfigBytes(file, cacheTableAssets);
                return new ByteBuf(bytes);
            });
        }

        /// <summary>同步加载 Luban 配置表原始字节。</summary>
        /// <param name="tableName">Luban 表名（如 <c>tbability</c>），内部通过 <see cref="ResourceAddresses.ConfigTable"/> 寻址。</param>
        /// <param name="cache">为 true 时缓存 TextAsset 句柄；须调用 <see cref="ReleaseTableAssetCache"/> 或 <see cref="Shutdown"/> 释放。</param>
        /// <returns>配置表原始字节；若 Asset 为 null 则返回空数组。</returns>
        /// <exception cref="InvalidOperationException">ResourceManager 未就绪或加载失败。</exception>
        public byte[] LoadConfigBytes(string tableName, bool cache = false)
        {
            EnsureResourceReady();
            var location = ResourceAddresses.ConfigTable(tableName);

            if (cache)
            {
                if (_tableAssetCache.TryGetValue(location, out var cached) && cached.IsValid)
                {
                    var cachedAsset = cached.GetAsset<TextAsset>();
                    return cachedAsset != null ? cachedAsset.bytes : Array.Empty<byte>();
                }

                var handle = ResourceManager.Instance.LoadAssetSync<TextAsset>(location);
                if (!handle.IsValid || !handle.Succeeded)
                {
                    handle.Dispose();
                    throw new InvalidOperationException(
                        $"[Config] Load config failed: {tableName}, location={location}, error={handle.Error}");
                }

                _tableAssetCache[location] = handle;
                var asset = handle.GetAsset<TextAsset>();
                return asset != null ? asset.bytes : Array.Empty<byte>();
            }

            using (var handle = ResourceManager.Instance.LoadAssetSync<TextAsset>(location))
            {
                if (!handle.IsValid || !handle.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"[Config] Load config failed: {tableName}, location={location}, error={handle.Error}");
                }

                var asset = handle.GetAsset<TextAsset>();
                return asset != null ? asset.bytes : Array.Empty<byte>();
            }
        }

        /// <summary>
        /// 获取已缓存 <see cref="CfgTables"/>；若尚未加载则先 <see cref="LoadTables"/>。
        /// </summary>
        /// <returns>Luban <see cref="CfgTables"/>。</returns>
        public CfgTables GetTables() => LoadTables();

        /// <summary>丢弃 <see cref="CfgTables"/> 对象缓存（不释放 TextAsset 句柄）。</summary>
        public void UnloadTables()
        {
            if (_tables == null)
            {
                return;
            }

            _tables = null;
            GameLog.Info(LogCategories.Config, "CfgTables cache cleared");
        }

        /// <summary>释放配置表 TextAsset 句柄缓存。</summary>
        public void ReleaseTableAssetCache()
        {
            foreach (var pair in _tableAssetCache)
            {
                pair.Value.Dispose();
            }

            _tableAssetCache.Clear();
        }

        /// <summary>
        /// 关闭配置管理：清空 <see cref="CfgTables"/> 并释放配置表 TextAsset 句柄缓存。
        /// </summary>
        public void Shutdown()
        {
            UnloadTables();
            ReleaseTableAssetCache();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor 直读 bin 目录并写入缓存（不走 YooAsset）；仅调试用。
        /// Player 请使用 <see cref="LoadTables"/>。
        /// </summary>
        /// <returns>Luban <see cref="CfgTables"/>。</returns>
        public CfgTables LoadEditorDefault()
        {
            _tables = ConfigLoader.LoadDefault();
            GameLog.Info(LogCategories.Config, $"CfgTables {LogStyle.Ok("loaded")} (editor bin)");
            return _tables;
        }
#endif

        void EnsureResourceReady()
        {
            if (!ResourceManager.HasInstance || !ResourceManager.Instance.IsInitialized)
            {
                throw new InvalidOperationException(
                    "ResourceManager is not ready. Initialize ResourceModule before loading config tables.");
            }
        }
    }
}
