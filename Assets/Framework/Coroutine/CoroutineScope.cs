namespace Framework.Coroutine
{
    /// <summary>协程生命周期作用域（不含 GameObject 绑定，GO 通过重载指定宿主）。</summary>
    public enum CoroutineScope
    {
        /// <summary>全局：跨场景常驻，仅手动 Stop 或模块 Shutdown 时结束。</summary>
        Global = 0,

        /// <summary>场景：随当前场景卸载自动全部停止。</summary>
        Scene = 1,
    }
}
