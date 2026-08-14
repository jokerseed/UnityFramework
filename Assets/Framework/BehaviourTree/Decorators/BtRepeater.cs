namespace Framework.BehaviourTree
{
    /// <summary>
    /// 重复执行子节点。times &lt;= 0 表示无限重复直到被 Abort。
    /// Failure 默认使装饰失败；<see cref="RepeatOnFailure"/> 为 true 时失败也重试。
    /// </summary>
    public sealed class BtRepeater : BtDecorator
    {
        readonly int _times;

        /// <summary>创建重复装饰。</summary>
        /// <param name="child">子节点；不可为 null。</param>
        /// <param name="times">重复次数；&lt;=0 表示无限。</param>
        /// <param name="repeatOnFailure">子节点 Failure 时是否重试。</param>
        public BtRepeater(BtNode child, int times = 0, bool repeatOnFailure = false) : base(child)
        {
            _times = times;
            RepeatOnFailure = repeatOnFailure;
        }

        /// <summary>子节点失败时是否当作一次完成并继续。</summary>
        public bool RepeatOnFailure { get; }

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            var runtime = context.Runtime;
            var count = runtime != null ? runtime.GetInt(Index) : 0;
            var status = Child.Tick(context);
            if (status == BtStatus.Running)
            {
                return Commit(context, BtStatus.Running);
            }

            if (status == BtStatus.Failure && !RepeatOnFailure)
            {
                Reset(context);
                return Commit(context, BtStatus.Failure);
            }

            count++;
            Child.Reset(context);
            runtime?.SetInt(Index, count);
            if (_times > 0 && count >= _times)
            {
                Reset(context);
                return Commit(context, BtStatus.Success);
            }

            return Commit(context, BtStatus.Running);
        }
    }
}
