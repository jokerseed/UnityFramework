namespace Framework.Core
{
    /// <summary>模块初始化执行方式。</summary>
    public enum ModuleInitMode
    {
        /// <summary>同步初始化，在当帧完成。</summary>
        Synchronous,

        /// <summary>异步初始化，由 Bootstrap 协程驱动。</summary>
        Asynchronous,
    }
}
