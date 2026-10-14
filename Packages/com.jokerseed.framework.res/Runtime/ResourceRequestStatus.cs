namespace Framework.Res
{
    /// <summary>调度队列中资源请求的状态。</summary>
    public enum ResourceRequestStatus
    {
        /// <summary>尚未入队或句柄无效。</summary>
        None = 0,

        /// <summary>已入队，等待本帧预算。</summary>
        Pending = 1,

        /// <summary>已发起，正在执行或等待 YooAsset 完成。</summary>
        Processing = 2,

        /// <summary>成功完成。</summary>
        Succeeded = 3,

        /// <summary>失败。</summary>
        Failed = 4,

        /// <summary>被取消（Shutdown 或主动 Cancel）。</summary>
        Cancelled = 5,
    }
}
