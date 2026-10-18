#if UNITY_EDITOR
using System;
using System.IO;
using YooAsset.Editor;

namespace Framework.Editor.YooAsset
{
    /// <summary>
    /// 对齐旧 AB 命名：Assets/Bundles/Configs/tbability.bytes → bundles/configs/tbability.unity3d
    /// </summary>
    [DisplayName("定位地址: Bundle路径")]
    public class AddressByPreImportPath : IAddressRule
    {
        const string AssetsPrefix = "Assets/";

        /// <summary>
        /// 将资源路径转换为 YooAsset 寻址字符串。
        /// 规则：去掉开头的 <c>Assets/</c>，把扩展名（最后一个点之后）换成 <c>.unity3d</c>，按固定区域规则转小写。
        /// 例：<c>Assets/Bundles/Configs/tbability.bytes</c> → <c>bundles/configs/tbability.unity3d</c>。
        /// 目录名或文件名里的其它点会保留（<c>v1.2/a.b.bytes</c> → <c>v1.2/a.b.unity3d</c>）；
        /// 转小写用 <see cref="string.ToLowerInvariant"/>，避免在土耳其语等系统上把 <c>I</c> 转成 <c>ı</c>。
        /// </summary>
        /// <param name="data">YooAsset 提供的寻址规则数据（含 AssetPath 等字段）。</param>
        /// <returns>规范化后的寻址字符串（全小写）。</returns>
        public string GetAssetAddress(AddressRuleData data)
        {
            var value = data.AssetPath.Replace('\\', '/');
            if (value.StartsWith(AssetsPrefix, StringComparison.Ordinal))
            {
                value = value.Substring(AssetsPrefix.Length);
            }

            return Path.ChangeExtension(value, ".unity3d").Replace('\\', '/').ToLowerInvariant();
        }
    }
}
#endif
