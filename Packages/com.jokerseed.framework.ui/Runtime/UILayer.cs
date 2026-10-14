namespace Framework.UI
{
    /// <summary>UI 层级，数值越大越靠前显示。</summary>
    public enum UILayer
    {
        /// <summary>底层背景界面（如战斗 HUD 底栏）。</summary>
        Bottom = 0,

        /// <summary>常规功能界面。</summary>
        UI = 100,

        /// <summary>顶层弹窗。</summary>
        Top = 200,

        /// <summary>提示、飘字等轻量提示层。</summary>
        Tips = 300,

        /// <summary>系统级遮罩、网络等待等最高层。</summary>
        System = 400,
    }
}
