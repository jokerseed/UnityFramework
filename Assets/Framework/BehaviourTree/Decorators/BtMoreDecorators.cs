using Framework.FixedMath;

namespace Framework.BehaviourTree
{
    /// <summary>强制失败：子节点非 Running 时一律返回 Failure。</summary>
    public sealed class BtForceFailure : BtDecorator
    {
        /// <summary>创建强制失败装饰。</summary>
        /// <param name="child">子节点；不可为 null。</param>
        public BtForceFailure(BtNode child) : base(child)
        {
        }

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            var status = Child.Tick(context);
            return Commit(context, status == BtStatus.Running ? BtStatus.Running : BtStatus.Failure);
        }
    }

    /// <summary>直到成功：子节点 Failure 则重置重试，Success 则成功。</summary>
    public sealed class BtUntilSuccess : BtDecorator
    {
        /// <summary>创建直到成功装饰。</summary>
        /// <param name="child">子节点；不可为 null。</param>
        public BtUntilSuccess(BtNode child) : base(child)
        {
        }

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            var status = Child.Tick(context);
            if (status == BtStatus.Running)
            {
                return Commit(context, BtStatus.Running);
            }

            if (status == BtStatus.Success)
            {
                Reset(context);
                return Commit(context, BtStatus.Success);
            }

            Child.Reset(context);
            return Commit(context, BtStatus.Running);
        }
    }

    /// <summary>冷却：子节点 Success 后在持续时间内再 Tick 直接 Failure。</summary>
    public sealed class BtCooldown : BtDecorator
    {
        readonly FP _duration;

        /// <summary>创建冷却装饰。</summary>
        /// <param name="child">子节点；不可为 null。</param>
        /// <param name="duration">冷却时长。</param>
        public BtCooldown(BtNode child, FP duration) : base(child)
        {
            _duration = duration < 0 ? FP.Zero : duration;
        }

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            var runtime = context.Runtime;
            var remaining = runtime != null ? runtime.GetFp(Index) : FP.Zero;
            if (remaining > 0)
            {
                remaining -= context.DeltaTime;
                runtime?.SetFp(Index, remaining);
                if (remaining > 0)
                {
                    return Commit(context, BtStatus.Failure);
                }
            }

            var status = Child.Tick(context);
            if (status == BtStatus.Success)
            {
                runtime?.SetFp(Index, _duration);
            }

            return Commit(context, status);
        }
    }

    /// <summary>超时：子节点 Running 超过时长则 Abort 并 Failure。</summary>
    public sealed class BtTimeout : BtDecorator
    {
        readonly FP _duration;

        /// <summary>创建超时装饰。</summary>
        /// <param name="child">子节点；不可为 null。</param>
        /// <param name="duration">最大 Running 时长。</param>
        public BtTimeout(BtNode child, FP duration) : base(child)
        {
            _duration = duration < 0 ? FP.Zero : duration;
        }

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            var runtime = context.Runtime;
            var elapsed = runtime != null ? runtime.GetFp(Index) : FP.Zero;
            var status = Child.Tick(context);
            if (status != BtStatus.Running)
            {
                Reset(context);
                return Commit(context, status);
            }

            elapsed += context.DeltaTime;
            runtime?.SetFp(Index, elapsed);
            if (elapsed >= _duration)
            {
                Child.Abort(context);
                Reset(context);
                return Commit(context, BtStatus.Failure);
            }

            return Commit(context, BtStatus.Running);
        }
    }

    /// <summary>时限：子节点 Running 超过时长则 Abort 并 Success。</summary>
    public sealed class BtTimeLimit : BtDecorator
    {
        readonly FP _duration;

        /// <summary>创建时限装饰。</summary>
        /// <param name="child">子节点；不可为 null。</param>
        /// <param name="duration">最大 Running 时长。</param>
        public BtTimeLimit(BtNode child, FP duration) : base(child)
        {
            _duration = duration < 0 ? FP.Zero : duration;
        }

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            var runtime = context.Runtime;
            var elapsed = runtime != null ? runtime.GetFp(Index) : FP.Zero;
            var status = Child.Tick(context);
            if (status != BtStatus.Running)
            {
                Reset(context);
                return Commit(context, status);
            }

            elapsed += context.DeltaTime;
            runtime?.SetFp(Index, elapsed);
            if (elapsed >= _duration)
            {
                Child.Abort(context);
                Reset(context);
                return Commit(context, BtStatus.Success);
            }

            return Commit(context, BtStatus.Running);
        }
    }

    /// <summary>子树包装：Tick 内联后的子根，便于调试名与 Abort 传递。</summary>
    public sealed class BtSubtree : BtDecorator
    {
        /// <summary>创建子树包装。</summary>
        /// <param name="child">子树根；不可为 null。</param>
        /// <param name="subtreeId">子树 id。</param>
        public BtSubtree(BtNode child, string subtreeId) : base(child)
        {
            TypeId = subtreeId;
        }

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            return Commit(context, Child.Tick(context));
        }
    }
}
