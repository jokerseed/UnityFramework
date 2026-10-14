using System;

namespace Framework.UI
{
    /// <summary>UI 资源寻址规则。</summary>
    public static class UIPaths
    {
        /// <summary>UI Prefab 根路径（对应 YooAsset Collector 中的 Assets/Bundles/UI）。</summary>
        public const string UIRoot = "Bundles/UI";

        /// <summary>
        /// 按窗口类型名生成默认寻址路径。
        /// 例如 <c>MainUIWindow</c> → <c>bundles/ui/mainuiwindow.unity3d</c>。
        /// </summary>
        /// <param name="windowType">窗口类型，不可为 null。</param>
        /// <returns>小写的 YooAsset 寻址字符串。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="windowType"/> 为 null。</exception>
        public static string Window(Type windowType)
        {
            if (windowType == null)
            {
                throw new ArgumentNullException(nameof(windowType));
            }

            return $"{UIRoot}/{windowType.Name}.unity3d".ToLowerInvariant();
        }
    }
}
