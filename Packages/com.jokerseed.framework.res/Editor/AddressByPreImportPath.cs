#if UNITY_EDITOR
using YooAsset.Editor;

namespace Framework.Editor.YooAsset
{
    /// <summary>
    /// 对齐旧 AB 命名：Assets/Bundles/Configs/tbability.bytes → bundles/configs/tbability.unity3d
    /// </summary>
    [DisplayName("定位地址: Bundle路径")]
    public class AddressByPreImportPath : IAddressRule
    {
        /// <summary>
        /// 将资源路径转换为 YooAsset 寻址字符串。
        /// 规则：去掉 <c>Assets/</c> 前缀，替换扩展名为 <c>.unity3d</c>，转全小写。
        /// 例：<c>Assets/Bundles/Configs/tbability.bytes</c> → <c>bundles/configs/tbability.unity3d</c>
        /// </summary>
        /// <param name="data">YooAsset 提供的寻址规则数据（含 AssetPath 等字段）。</param>
        /// <returns>规范化后的寻址字符串（全小写）。</returns>
        public string GetAssetAddress(AddressRuleData data)
        {
            var value = data.AssetPath.Replace("Assets/", string.Empty);
            var dotIndex = value.IndexOf('.');
            if (dotIndex >= 0)
            {
                value = value.Substring(0, dotIndex);
            }

            return (value + ".unity3d").ToLower();
        }
    }
}
#endif
