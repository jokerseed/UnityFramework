namespace Framework.BehaviourTree
{
    /// <summary>
    /// 重复执行子节点。times &lt;= 0 表示无限重复直到被 Abort。
    /// </summary>
    public sealed class BtRepeater : BtDecorator
    {
        readonly int _times;
        int _count;
        bool _started;

        /// <summary>
        /// 创建重复装饰。
        /// </summary>
        /// <param name="child">子节点；不可为 null。</param>
        /// <param name="times">重复次数；&lt;=0 表示无限。</param>
        public BtRepeater(BtNode child, int times = 0) : base(child)
        {
            _times = times;
        }

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            if (!_started)
            {
                _count = 0;
                _started = true;
            }

            var status = Child.Tick(context);
            if (status == BtStatus.Running)
            {
                return BtStatus.Running;
            }

            if (status == BtStatus.Failure)
            {
                Reset(context);
                return BtStatus.Failure;
            }

            _count++;
            Child.Reset(context);
            if (_times > 0 && _count >= _times)
            {
                Reset(context);
                return BtStatus.Success;
            }

            return BtStatus.Running;
        }

        /// <inheritdoc />
        public override void Reset(BtContext context)
        {
            _count = 0;
            _started = false;
            base.Reset(context);
        }
    }
}
