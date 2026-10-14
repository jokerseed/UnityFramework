using System;

namespace Framework.UI
{
    /// <summary>
    /// 标记 <see cref="UIWindow"/> 的层级、全屏行为、资源寻址与关闭释放策略。
    /// 未标记时使用默认层级 <see cref="UILayer.UI"/>，并按类型名自动生成寻址路径。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class UIWindowAttribute : Attribute
    {
        /// <summary>窗口所在 UI 层级。</summary>
        public UILayer Layer { get; }

        /// <summary>是否为全屏窗口；为 true 时会遮挡同层及以下已打开窗口的显示。</summary>
        public bool FullScreen { get; }

        /// <summary>
        /// YooAsset 寻址路径；为 null 或空时由 <see cref="UIPaths"/> 按窗口类型名生成。
        /// </summary>
        public string Location { get; }

        /// <summary>关闭时的释放策略。</summary>
        public UIReleasePolicy ReleasePolicy { get; }

        /// <summary>
        /// <see cref="UIReleasePolicy.HideAndDelayUnload"/> 的延迟秒数；其他策略忽略。
        /// </summary>
        public float DelayUnloadSeconds { get; }

        /// <summary>创建窗口特性。</summary>
        /// <param name="layer">UI 层级，默认 <see cref="UILayer.UI"/>。</param>
        /// <param name="fullScreen">是否全屏遮挡下层窗口。</param>
        /// <param name="location">资源寻址；为 null 时按类型名自动生成。</param>
        /// <param name="releasePolicy">关闭释放策略；默认 <see cref="UIReleasePolicy.DestroyImmediate"/>。</param>
        /// <param name="delayUnloadSeconds">延迟卸载秒数；仅 <see cref="UIReleasePolicy.HideAndDelayUnload"/> 有效。</param>
        public UIWindowAttribute(
            UILayer layer = UILayer.UI,
            bool fullScreen = false,
            string location = null,
            UIReleasePolicy releasePolicy = UIReleasePolicy.DestroyImmediate,
            float delayUnloadSeconds = 30f)
        {
            Layer = layer;
            FullScreen = fullScreen;
            Location = location;
            ReleasePolicy = releasePolicy;
            DelayUnloadSeconds = delayUnloadSeconds > 0f ? delayUnloadSeconds : 30f;
        }
    }
}
