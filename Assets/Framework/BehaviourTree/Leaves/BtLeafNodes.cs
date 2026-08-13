using System;
using Framework.FixedMath;

namespace Framework.BehaviourTree
{
    /// <summary>等待固定逻辑帧数后成功。</summary>
    public sealed class BtWaitFrames : BtNode
    {
        readonly int _frames;
        int _remaining;
        bool _started;

        /// <summary>
        /// 创建按帧等待节点。
        /// </summary>
        /// <param name="frames">等待的逻辑帧数；必须 &gt;= 0。0 表示当帧立即成功。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="frames"/> 为负数。</exception>
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
            if (!_started)
            {
                _remaining = _frames;
                _started = true;
            }

            if (_remaining <= 0)
            {
                Reset(context);
                return BtStatus.Success;
            }

            _remaining--;
            if (_remaining <= 0)
            {
                Reset(context);
                return BtStatus.Success;
            }

            return BtStatus.Running;
        }

        /// <inheritdoc />
        public override void Reset(BtContext context)
        {
            _remaining = 0;
            _started = false;
        }
    }

    /// <summary>按定点时间累计等待；使用 <see cref="BtContext.DeltaTime"/>，禁止真实时间。</summary>
    public sealed class BtWaitTime : BtNode
    {
        readonly FP _duration;
        FP _elapsed;
        bool _started;

        /// <summary>
        /// 创建按时间等待节点。
        /// </summary>
        /// <param name="duration">等待时长（与 <see cref="BtContext.DeltaTime"/> 同单位）；必须 &gt;= 0。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> 为负数。</exception>
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
            if (!_started)
            {
                _elapsed = 0;
                _started = true;
            }

            _elapsed += context.DeltaTime;
            if (_elapsed >= _duration)
            {
                Reset(context);
                return BtStatus.Success;
            }

            return BtStatus.Running;
        }

        /// <inheritdoc />
        public override void Reset(BtContext context)
        {
            _elapsed = 0;
            _started = false;
        }
    }

    /// <summary>
    /// 委托动作：由外部在权威逻辑中实现；返回值须确定性。
    /// </summary>
    public sealed class BtAction : BtNode
    {
        readonly Func<BtContext, BtStatus> _tick;
        readonly Action<BtContext> _onReset;

        /// <summary>
        /// 创建委托动作节点。
        /// </summary>
        /// <param name="tick">每逻辑帧回调；不可为 null。</param>
        /// <param name="onReset">重置回调；可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="tick"/> 为 null。</exception>
        public BtAction(Func<BtContext, BtStatus> tick, Action<BtContext> onReset = null)
        {
            _tick = tick ?? throw new ArgumentNullException(nameof(tick));
            _onReset = onReset;
        }

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context) => _tick(context);

        /// <inheritdoc />
        public override void Reset(BtContext context) => _onReset?.Invoke(context);
    }

    /// <summary>
    /// 委托条件：返回 Success/Failure（Running 会被规范为 Failure）。
    /// </summary>
    public sealed class BtCondition : BtNode
    {
        readonly Func<BtContext, bool> _predicate;

        /// <summary>
        /// 创建委托条件节点。
        /// </summary>
        /// <param name="predicate">条件判定；不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="predicate"/> 为 null。</exception>
        public BtCondition(Func<BtContext, bool> predicate)
        {
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        }

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            return _predicate(context) ? BtStatus.Success : BtStatus.Failure;
        }
    }
}
