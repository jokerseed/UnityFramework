#if UNITY_EDITOR
using System;
using System.IO;
using YooAsset.Editor;

namespace Framework.Editor.YooAsset
{
    /// <summary>
    /// 只收集收集目录本层的文件，不收集子目录。
    /// 父目录与各子目录分别建收集器时，父目录用它，避免同一资源被两个收集器收集（YooAsset 会直接报错）。
    /// </summary>
    [DisplayName("收集本层资源（不含子目录）")]
    public class CollectTopDirectoryOnly : IAssetFilterRule
    {
        /// <inheritdoc/>
        public string FindAssetType => EAssetFilterType.All.ToString();

        /// <inheritdoc/>
        public bool IsCollectAsset(AssetFilterRuleData data)
        {
            var directory = Path.GetDirectoryName(data.AssetPath)?.Replace('\\', '/');
            var collectPath = data.CollectPath?.TrimEnd('/');
            return string.Equals(directory, collectPath, StringComparison.Ordinal);
        }
    }
}
#endif
