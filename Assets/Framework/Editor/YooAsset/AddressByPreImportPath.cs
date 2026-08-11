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
