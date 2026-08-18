namespace Framework.UI
{
    /// <summary>窗口关闭时的资源与实例释放策略。</summary>
    public enum UIReleasePolicy
    {
        /// <summary>关闭时立即销毁实例并释放 Prefab 句柄（默认）。</summary>
        DestroyImmediate = 0,

        /// <summary>关闭时仅隐藏并移出栈，实例与句柄仍登记在 <see cref="UIManager"/>（<see cref="IsOpen{TWindow}"/> 仍为 true）；再次 Show 走已打开分支，不经过缓存复用。</summary>
        HideOnly = 1,

        /// <summary>关闭时移出栈与已打开表并缓存实例；再次 Show 经 <c>TryReviveCached</c> 快速复用，不重新 Load。</summary>
        Cached = 2,

        /// <summary>关闭时先隐藏并缓存；若在延迟时间内未再次打开则自动销毁并释放句柄。</summary>
        HideAndDelayUnload = 3,
    }
}
