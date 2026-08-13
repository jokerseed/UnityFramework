using cfg;
using Framework.Core;
using Framework.Logging;
using Framework.Res;
using UnityEngine;

namespace Framework.Config
{
    /// <summary>
    /// 配置管理器：Luban <see cref="CfgTables"/> 的按需加载与缓存入口。
    /// 不在模块初始化时读表；业务首次需要时调用 <see cref="LoadTables"/>。
    /// </summary>
    [DefaultExecutionOrder(-450)]
    public sealed class ConfigManager : PersistentSingleton<ConfigManager>
    {
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
        /// 底层通过 <see cref="ResourceManager.LoadLubanTables"/> 读 Bundle。
        /// </summary>
        /// <param name="cacheTableAssets">是否缓存各表 TextAsset 句柄（交给 ResourceManager）。</param>
        /// <returns>Luban <see cref="CfgTables"/>。</returns>
        /// <exception cref="InvalidOperationException">ResourceManager 未就绪或加载失败。</exception>
        public CfgTables LoadTables(bool cacheTableAssets = true)
        {
            if (_tables != null)
            {
                return _tables;
            }

            if (!ResourceManager.HasInstance || !ResourceManager.Instance.IsInitialized)
            {
                throw new System.InvalidOperationException(
                    "ResourceManager is not ready. Initialize ResourceModule before loading config tables.");
            }

            _tables = ResourceManager.Instance.LoadLubanTables(cacheTableAssets);
            GameLog.Info(LogCategories.Config, $"CfgTables {LogStyle.Ok("loaded")} (cached)");
            return _tables;
        }

        /// <summary>
        /// 获取已缓存 <see cref="CfgTables"/>；若尚未加载则先 <see cref="LoadTables"/>。
        /// </summary>
        /// <returns>Luban <see cref="CfgTables"/>。</returns>
        public CfgTables GetTables() => LoadTables();

        /// <summary>丢弃 <see cref="CfgTables"/> 缓存（不销毁 ResourceManager；表字节缓存仍由 Res 管理）。</summary>
        public void UnloadTables()
        {
            if (_tables == null)
            {
                return;
            }

            _tables = null;
            GameLog.Info(LogCategories.Config, "CfgTables cache cleared");
        }

        /// <summary>
        /// 关闭配置管理：清空 <see cref="CfgTables"/>，并释放 ResourceManager 中同步资源缓存（含配置 TextAsset）。
        /// </summary>
        public void Shutdown()
        {
            UnloadTables();

            if (ResourceManager.HasInstance && ResourceManager.Instance.IsInitialized)
            {
                ResourceManager.Instance.ReleaseCache();
            }
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
    }
}
