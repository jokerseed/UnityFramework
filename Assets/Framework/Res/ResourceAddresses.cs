namespace Framework.Res
{
    public static class ResourceAddresses
    {
        public const string ConfigsRoot = "Bundles/Configs";

        /// <summary>
        /// Luban 表名 → YooAsset 寻址（与 AddressByPreImportPath 一致）。
        /// tbability → bundles/configs/tbability.unity3d
        /// </summary>
        public static string ConfigTable(string tableName)
        {
            return $"{ConfigsRoot}/{tableName}.unity3d".ToLower();
        }
    }
}
