namespace Framework.BehaviourTree
{
    /// <summary>取反：Success↔Failure；Running 保持。</summary>
    public sealed class BtInverter : BtDecorator
    {
        /// <summary>
        /// 创建取反装饰。
        /// </summary>
        /// <param name="child">子节点；不可为 null。</param>
        public BtInverter(BtNode child) : base(child)
        {
        }

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            var status = Child.Tick(context);
            switch (status)
            {
                case BtStatus.Success:
                    return Commit(context, BtStatus.Failure);
                case BtStatus.Failure:
                    return Commit(context, BtStatus.Success);
                default:
                    return Commit(context, BtStatus.Running);
            }
        }
    }
}
