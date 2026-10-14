namespace Framework.BehaviourTree
{
    /// <summary>并行策略：如何汇总子节点本帧结果。</summary>
    public enum BtParallelPolicy
    {
        /// <summary>全部成功才成功；失败是否立刻返回由 <see cref="BtParallel.FailFast"/> 决定。</summary>
        RequireAll = 0,

        /// <summary>任一成功即成功；成功是否立刻返回由 <see cref="BtParallel.SucceedFast"/> 决定。</summary>
        RequireOne = 1,
    }

    /// <summary>并行节点：每帧对尚未结束的子节点 Tick，按策略汇总。</summary>
    public sealed class BtParallel : BtComposite
    {
        /// <summary>创建并行节点。</summary>
        /// <param name="policy">汇总策略。</param>
        /// <param name="failFast">任一失败是否立刻失败并中止其余；默认 true。</param>
        /// <param name="succeedFast">任一成功是否立刻成功并中止其余；默认 true。</param>
        public BtParallel(
            BtParallelPolicy policy = BtParallelPolicy.RequireAll,
            bool failFast = true,
            bool succeedFast = true)
        {
            Policy = policy;
            FailFast = failFast;
            SucceedFast = succeedFast;
        }

        /// <summary>汇总策略。</summary>
        public BtParallelPolicy Policy { get; }

        /// <summary>出现失败时是否立刻失败并 Abort 仍在 Running 的子节点。</summary>
        public bool FailFast { get; }

        /// <summary>出现成功时是否立刻成功并 Abort 仍在 Running 的子节点。</summary>
        public bool SucceedFast { get; }

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            var runtime = context.Runtime;
            if (runtime != null && !runtime.IsStarted(Index))
            {
                for (var i = 0; i < Children.Count; i++)
                {
                    runtime.SetStatus(Children[i].Index, BtStatus.Running);
                }

                runtime.SetStarted(Index, true);
            }

            var anyRunning = false;
            var anySuccess = false;
            var anyFailure = false;

            for (var i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                var childStatus = runtime != null ? runtime.GetStatus(child.Index) : BtStatus.Running;
                if (childStatus == BtStatus.Running)
                {
                    childStatus = child.Tick(context);
                }

                switch (childStatus)
                {
                    case BtStatus.Running:
                        anyRunning = true;
                        break;
                    case BtStatus.Success:
                        anySuccess = true;
                        break;
                    case BtStatus.Failure:
                        anyFailure = true;
                        break;
                }
            }

            if (Policy == BtParallelPolicy.RequireAll)
            {
                if (anyFailure && (FailFast || !anyRunning))
                {
                    AbortRunningChildren(context);
                    Reset(context);
                    return Commit(context, BtStatus.Failure);
                }

                if (anyRunning)
                {
                    return Commit(context, BtStatus.Running);
                }

                Reset(context);
                return Commit(context, BtStatus.Success);
            }

            if (anySuccess && (SucceedFast || !anyRunning))
            {
                AbortRunningChildren(context);
                Reset(context);
                return Commit(context, BtStatus.Success);
            }

            if (anyRunning)
            {
                return Commit(context, BtStatus.Running);
            }

            Reset(context);
            return Commit(context, BtStatus.Failure);
        }

        void AbortRunningChildren(BtContext context)
        {
            var runtime = context.Runtime;
            for (var i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                if (runtime == null || runtime.GetStatus(child.Index) == BtStatus.Running)
                {
                    child.Abort(context);
                }
            }
        }
    }
}
