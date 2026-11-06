using UnityEngine;

namespace Framework.UI
{
    /// <summary>将窗口模型结果应用到 Unity；不持有窗口注册表或资源所有权。</summary>
    internal static class WindowUnityAdapter
    {
        internal static void ApplySorting(WindowLayout<UIWindow> layout)
        {
            var window = layout.Window;
            window.SortingOrder = layout.SortingOrder;
            var canvas = window.GameObject != null ? window.GameObject.GetComponent<Canvas>() : null;
            if (canvas != null) canvas.sortingOrder = layout.SortingOrder;
        }

        internal static void ApplyVisibility(WindowLayout<UIWindow> layout)
        {
            layout.Window.SetVisible(true);
            layout.Window.SetOccluded(layout.Occluded);
        }
    }
}
