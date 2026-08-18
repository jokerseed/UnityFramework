namespace Framework.UI
{
    /// <summary>窗口关闭时的资源与实例释放策略。</summary>
    public enum UIReleasePolicy
    {
        /// <summary>关闭时立即销毁实例并释放 Prefab 句柄（默认）。</summary>
        DestroyImmediate = 0,

        /// <summary>关闭时仅隐藏，保留实例与句柄直到再次 Show 或模块 Shutdown。</summary>
        HideOnly = 1,

        /// <summary>关闭时移出栈并缓存实例；再次 Show 直接复用，不重新 Load。</summary>
        Cached = 2,

        /// <summary>关闭时先隐藏并缓存；若在延迟时间内未再次打开则自动销毁并释放句柄。</summary>
        HideAndDelayUnload = 3,
    }
}
