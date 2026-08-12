using System.IO;
using UnityEngine;

namespace Framework.Config
{
    /// <summary>Luban 配置二进制资源路径常量。</summary>
    public static class ConfigPaths
    {
        /// <summary>相对 Assets 的配置 bin 目录。</summary>
        public const string RelativeBinDir = "Bundles/Configs";

        /// <summary>获取 Editor 直读用的配置 bin 绝对目录。</summary>
        /// <returns>绝对路径。</returns>
        public static string GetBinDirectory()
        {
            return Path.Combine(Application.dataPath, RelativeBinDir);
        }
    }
}
