namespace Framework.BehaviourTree
{
    /// <summary>强制成功：子节点非 Running 时一律返回 Success。</summary>
    public sealed class BtForceSuccess : BtDecorator
    {
        /// <summary>
        /// 创建强制成功装饰。
        /// </summary>
        /// <param name="child">子节点；不可为 null。</param>
        public BtForceSuccess(BtNode child) : base(child)
        {
        }

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            var status = Child.Tick(context);
            return Commit(context, status == BtStatus.Running ? BtStatus.Running : BtStatus.Success);
        }
    }
}
