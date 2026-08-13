namespace Framework.BehaviourTree
{
    /// <summary>行为树节点基类。实例状态归属单个 Agent，不可多 Agent 共用同一节点实例。</summary>
    public abstract class BtNode
    {
        /// <summary>节点调试名；可为 null。</summary>
        public string Name { get; set; }

        /// <summary>
        /// 在本逻辑帧执行节点。
        /// </summary>
        /// <param name="context">Tick 上下文；不可为 null。</param>
        /// <returns>本帧执行状态。</returns>
        public abstract BtStatus Tick(BtContext context);

        /// <summary>
        /// 重置节点内部运行时状态（子节点索引、等待剩余等）。
        /// </summary>
        /// <param name="context">Tick 上下文；不可为 null。</param>
        public virtual void Reset(BtContext context)
        {
        }

        /// <summary>
        /// 中止当前 Running 分支时调用；默认转发到 <see cref="Reset"/>。
        /// </summary>
        /// <param name="context">Tick 上下文；不可为 null。</param>
        public virtual void Abort(BtContext context)
        {
            Reset(context);
        }
    }
}
