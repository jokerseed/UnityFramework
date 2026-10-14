using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Logging;
using Framework.Res;
using Luban;
using UnityEngine;

namespace Framework.Config
{
    /// <summary>
    /// 配置管理器：Luban 生成的表聚合类（如 <c>cfg.CfgTables</c>）的按需加载与缓存入口。
    /// 框架不认识具体表类型：游戏通过 <see cref="RegisterTables{TTables}"/>（或 <see cref="ConfigModule.Create{TTables}"/>）注册构造工厂，
    /// 之后用 <see cref="LoadTables{TTables}"/> / <see cref="GetTables{TTables}"/> 读取。
    /// 配置表 Bundle 的加载/释放逻辑归属本类，底层仅调用 <see cref="ResourceManager"/> 通用 API。
    /// </summary>
    [DefaultExecutionOrder(-450)]
    public sealed class ConfigManager : PersistentSingleton<ConfigManager>
    {
        readonly Dictionary<string, ResourceAssetHandle> _tableAssetCache = new Dictionary<string, ResourceAssetHandle>();

        Func<Func<string, ByteBuf>, object> _factory;
        Type _tablesType;
        object _tables;

        /// <summary>表缓存被 <see cref="ReloadTables"/> 重建后触发；参数为新的表对象。</summary>
        public event Action<object> TablesReloaded;

        /// <summary>是否已注册表工厂。</summary>
        public bool IsRegistered => _factory != null;

        /// <summary>已注册的表聚合类型；未注册时为 null。</summary>
        public Type TablesType => _tablesType;

        /// <summary>是否已加载并缓存表对象。</summary>
        public bool IsLoaded => _tables != null;

        /// <summary>
        /// 注册 Luban 表聚合类的构造工厂（通常是 <c>loader =&gt; new cfg.CfgTables(loader)</c>）。
        /// 重复注册同一类型会替换工厂并丢弃已缓存的表对象。
        /// </summary>
        /// <typeparam name="TTables">Luban 生成的表聚合类型。</typeparam>
        /// <param name="factory">接收"表名 → <see cref="ByteBuf"/>"读取器并返回表对象的工厂。</param>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">已注册了另一种表类型。</exception>
        public void RegisterTables<TTables>(Func<Func<string, ByteBuf>, TTables> factory) where TTables : class
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (_tablesType != null && _tablesType != typeof(TTables))
            {
                throw new InvalidOperationException(
                    $"[Config] Tables type already registered as {_tablesType.FullName}; cannot register {typeof(TTables).FullName}.");
            }

            _tablesType = typeof(TTables);
            _factory = loader => factory(loader);
            _tables = null;
        }

        /// <summary>
        /// 按需加载全表并缓存；已加载则直接返回缓存。
        /// </summary>
        /// <typeparam name="TTables">注册时使用的表聚合类型。</typeparam>
        /// <param name="cacheTableAssets">是否缓存各表 TextAsset 句柄（由本类 table asset 缓存管理）。</param>
        /// <returns>表对象。</returns>
        /// <exception cref="InvalidOperationException">未注册、类型不符、ResourceManager 未就绪或加载失败。</exception>
        public TTables LoadTables<TTables>(bool cacheTableAssets = true) where TTables : class
        {
            EnsureType<TTables>();
            if (_tables != null)
            {
                return (TTables)_tables;
            }

            _tables = CreateTables<TTables>(cacheTableAssets);
            GameLog.Info(LogCategories.Config, $"{_tablesType.Name} {LogStyle.Ok("loaded")} (cached)");
            return (TTables)_tables;
        }

        /// <summary>
        /// 从 Bundle 构造一份新的表对象；不写入缓存。需要独立副本（如对比、工具）时使用。
        /// </summary>
        /// <typeparam name="TTables">注册时使用的表聚合类型。</typeparam>
        /// <param name="cacheTableAssets">是否缓存各表 TextAsset 句柄。</param>
        /// <returns>新的表对象。</returns>
        /// <exception cref="InvalidOperationException">未注册、类型不符、ResourceManager 未就绪或加载失败。</exception>
        public TTables CreateTables<TTables>(bool cacheTableAssets = true) where TTables : class
        {
            EnsureType<TTables>();
            EnsureResourceReady();
            return (TTables)_factory(file => new ByteBuf(LoadConfigBytes(file, cacheTableAssets)));
        }

        /// <summary>获取已缓存的表对象；若尚未加载则先 <see cref="LoadTables{TTables}"/>。</summary>
        /// <typeparam name="TTables">注册时使用的表聚合类型。</typeparam>
        /// <returns>表对象。</returns>
        /// <exception cref="InvalidOperationException">未注册、类型不符或加载失败。</exception>
        public TTables GetTables<TTables>() where TTables : class => LoadTables<TTables>();

        /// <summary>只读取已缓存的表对象，不触发加载。</summary>
        /// <typeparam name="TTables">注册时使用的表聚合类型。</typeparam>
        /// <param name="tables">已缓存时为表对象，否则为 null。</param>
        /// <returns>已缓存且类型匹配时为 true。</returns>
        public bool TryGetTables<TTables>(out TTables tables) where TTables : class
        {
            tables = _tables as TTables;
            return tables != null;
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
                    var error = handle.Error;
                    handle.Dispose();
                    throw new InvalidOperationException(
                        $"[Config] Load config failed: {tableName}, location={location}, error={error}");
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
        /// 丢弃表对象与 TextAsset 句柄缓存后重新加载，并触发 <see cref="TablesReloaded"/>。
        /// 用于 Editor 下改表后热重载；已持有旧表引用的业务需订阅事件自行替换。
        /// </summary>
        /// <returns>新的表对象。</returns>
        /// <exception cref="InvalidOperationException">未注册或加载失败。</exception>
        public object ReloadTables()
        {
            if (_factory == null)
            {
                throw new InvalidOperationException("[Config] No tables registered. Call RegisterTables first.");
            }

            UnloadTables();
            ReleaseTableAssetCache();
            EnsureResourceReady();
            _tables = _factory(file => new ByteBuf(LoadConfigBytes(file, true)));
            GameLog.Info(LogCategories.Config, $"{_tablesType.Name} {LogStyle.Ok("reloaded")}");
            TablesReloaded?.Invoke(_tables);
            return _tables;
        }

        /// <summary>丢弃表对象缓存（不释放 TextAsset 句柄）。</summary>
        public void UnloadTables()
        {
            if (_tables == null)
            {
                return;
            }

            _tables = null;
            GameLog.Info(LogCategories.Config, "Tables cache cleared");
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
        /// 关闭配置管理：清空表对象并释放配置表 TextAsset 句柄缓存；保留已注册的工厂。
        /// </summary>
        public void Shutdown()
        {
            UnloadTables();
            ReleaseTableAssetCache();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor 直读默认 bin 目录并写入缓存（不走 YooAsset）；仅调试用。
        /// Player 请使用 <see cref="LoadTables{TTables}"/>。
        /// </summary>
        /// <typeparam name="TTables">注册时使用的表聚合类型。</typeparam>
        /// <returns>表对象。</returns>
        /// <exception cref="InvalidOperationException">未注册或类型不符。</exception>
        public TTables LoadEditorDefault<TTables>() where TTables : class
        {
            EnsureType<TTables>();
            _tables = ConfigLoader.LoadBinaryFromDirectory(loader => (TTables)_factory(loader), ConfigPaths.GetBinDirectory());
            GameLog.Info(LogCategories.Config, $"{_tablesType.Name} {LogStyle.Ok("loaded")} (editor bin)");
            return (TTables)_tables;
        }
#endif

        void EnsureType<TTables>()
        {
            if (_factory == null)
            {
                throw new InvalidOperationException(
                    $"[Config] No tables registered. Call ConfigManager.RegisterTables<{typeof(TTables).Name}>() or use ConfigModule.Create<{typeof(TTables).Name}>().");
            }

            if (_tablesType != typeof(TTables))
            {
                throw new InvalidOperationException(
                    $"[Config] Registered tables type is {_tablesType.FullName}, requested {typeof(TTables).FullName}.");
            }
        }

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
