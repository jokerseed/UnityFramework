namespace Framework.Res
{
    /// <summary>资源加载操作状态。</summary>
    public enum ResourceLoadStatus
    {
        /// <summary>未开始或无效。</summary>
        None = 0,

        /// <summary>加载中。</summary>
        Processing = 1,

        /// <summary>加载成功。</summary>
        Succeeded = 2,

        /// <summary>加载失败。</summary>
        Failed = 3,
    }
}
