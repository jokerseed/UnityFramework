namespace Framework.Res
{
    /// <summary>集中定义 YooAsset 资源寻址路径，避免业务代码中出现散落的路径字符串。</summary>
    public static class ResourceAddresses
    {
        /// <summary>配置表资源根路径（对应 YooAsset Collector 中的 Assets/Bundles/Configs）。</summary>
        public const string ConfigsRoot = "Bundles/Configs";

        /// <summary>Prefab 资源根路径（对应 YooAsset Collector 中的 Assets/Bundles/Prefabs）。</summary>
        public const string PrefabsRoot = "Bundles/Prefabs";

        /// <summary>场景资源根路径（对应 YooAsset Collector 中的 Assets/Bundles/Scenes）。</summary>
        public const string ScenesRoot = "Bundles/Scenes";

        /// <summary>首页 Prefab 寻址路径（Assets/Bundles/Prefabs/UI/Main.prefab）。</summary>
        public const string MainPrefab = "bundles/prefabs/ui/main.unity3d";

        /// <summary>启动场景寻址路径（Assets/Bundles/Scenes/Launch.unity）。</summary>
        public const string LaunchScene = "bundles/scenes/launch.unity3d";

        /// <summary>
        /// 将 Luban 表名转换为 YooAsset 寻址字符串（与 AddressByPreImportPath 规则一致）。
        /// 例如：<c>tbability</c> → <c>bundles/configs/tbability.unity3d</c>
        /// </summary>
        /// <param name="tableName">Luban 表名（不含扩展名，如 <c>tbability</c>）。</param>
        /// <returns>对应的 YooAsset 寻址字符串（全小写）。</returns>
        public static string ConfigTable(string tableName)
        {
            return $"{ConfigsRoot}/{tableName}.unity3d".ToLower();
        }

        /// <summary>
        /// 将场景名转换为 YooAsset 寻址字符串（与 AddressByPreImportPath 规则一致）。
        /// 例如：<c>Launch</c> → <c>bundles/scenes/launch.unity3d</c>
        /// </summary>
        /// <param name="sceneName">场景名（不含扩展名，如 <c>Launch</c>）。</param>
        /// <returns>对应的 YooAsset 寻址字符串（全小写）。</returns>
        public static string Scene(string sceneName)
        {
            return $"{ScenesRoot}/{sceneName}.unity3d".ToLower();
        }
    }
}
