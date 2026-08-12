namespace Framework.Res
{
    /// <summary>集中定义 YooAsset 资源寻址路径，避免业务代码中出现散落的路径字符串。</summary>
    public static class ResourceAddresses
    {
        /// <summary>配置表资源根路径（对应 YooAsset Collector 中的 Assets/Bundles/Configs）。</summary>
        public const string ConfigsRoot = "Bundles/Configs";

        /// <summary>首页 Prefab 寻址路径。</summary>
        public const string MainPrefab = "bundles/prefabs/main.unity3d";

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
    }
}
