using System;
using Framework.FixedMath;

namespace Framework.BehaviourTree
{
    /// <summary>等待固定逻辑帧数后成功。进度在 Runtime 槽。</summary>
    public sealed class BtWaitFrames : BtNode
    {
        readonly int _frames;

        /// <summary>创建按帧等待节点。</summary>
        /// <param name="frames">等待的逻辑帧数；必须 &gt;= 0。</param>
        public BtWaitFrames(int frames)
        {
            if (frames < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frames), "Frame count must be >= 0.");
            }

            _frames = frames;
        }

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            var runtime = context.Runtime;
            var remaining = runtime != null && runtime.IsStarted(Index)
                ? runtime.GetInt(Index)
                : _frames;

            runtime?.SetStarted(Index, true);
            if (remaining <= 0)
            {
                Reset(context);
                return Commit(context, BtStatus.Success);
            }

            remaining--;
            runtime?.SetInt(Index, remaining);
            if (remaining <= 0)
            {
                Reset(context);
                return Commit(context, BtStatus.Success);
            }

            return Commit(context, BtStatus.Running);
        }
    }

    /// <summary>按定点时间累计等待；使用 <see cref="BtContext.DeltaTime"/>。</summary>
    public sealed class BtWaitTime : BtNode
    {
        readonly FP _duration;

        /// <summary>创建按时间等待节点。</summary>
        /// <param name="duration">等待时长；必须 &gt;= 0。</param>
        public BtWaitTime(FP duration)
        {
            if (duration < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be >= 0.");
            }

            _duration = duration;
        }

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            var runtime = context.Runtime;
            var elapsed = runtime != null && runtime.IsStarted(Index)
                ? runtime.GetFp(Index)
                : FP.Zero;

            runtime?.SetStarted(Index, true);
            elapsed += context.DeltaTime;
            runtime?.SetFp(Index, elapsed);
            if (elapsed >= _duration)
            {
                Reset(context);
                return Commit(context, BtStatus.Success);
            }

            return Commit(context, BtStatus.Running);
        }
    }

    /// <summary>委托动作：由外部在权威逻辑中实现；返回值须确定性。</summary>
    public sealed class BtAction : BtNode
    {
        readonly Func<BtContext, BtStatus> _tick;
        readonly Action<BtContext> _onReset;
        readonly Action<BtContext> _onAbort;

        /// <summary>创建委托动作节点。</summary>
        /// <param name="tick">每逻辑帧回调；不可为 null。</param>
        /// <param name="onReset">重置回调；可为 null。</param>
        /// <param name="onAbort">中止回调（先于 Reset）；可为 null。Running 被打断时必须在此收尾。</param>
        public BtAction(
            Func<BtContext, BtStatus> tick,
            Action<BtContext> onReset = null,
            Action<BtContext> onAbort = null)
        {
            _tick = tick ?? throw new ArgumentNullException(nameof(tick));
            _onReset = onReset;
            _onAbort = onAbort;
        }

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context) => Commit(context, _tick(context));

        /// <inheritdoc />
        public override void Reset(BtContext context)
        {
            _onReset?.Invoke(context);
            ClearSlot(context);
        }

        /// <inheritdoc />
        public override void Abort(BtContext context)
        {
            _onAbort?.Invoke(context);
            Reset(context);
        }
    }

    /// <summary>委托条件：返回 Success/Failure。</summary>
    public sealed class BtCondition : BtNode
    {
        readonly Func<BtContext, bool> _predicate;

        /// <summary>创建委托条件节点。</summary>
        /// <param name="predicate">条件判定；不可为 null。</param>
        public BtCondition(Func<BtContext, bool> predicate)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            return Commit(context, _predicate(context) ? BtStatus.Success : BtStatus.Failure);
        }
    }
}
