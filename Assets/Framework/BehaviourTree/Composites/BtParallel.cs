using System;

namespace Framework.BehaviourTree
{
    /// <summary>并行策略：如何汇总子节点本帧结果。</summary>
    public enum BtParallelPolicy
    {
        /// <summary>全部成功才成功；任一失败则失败；否则 Running。</summary>
        RequireAll = 0,

        /// <summary>任一成功即成功；全部失败才失败；否则 Running。</summary>
        RequireOne = 1,
    }

    /// <summary>
    /// 并行节点：每帧对所有仍在进行的子节点 Tick，按策略汇总。
    /// </summary>
    public sealed class BtParallel : BtComposite
    {
        BtStatus[] _statuses;
        bool _started;

        /// <summary>
        /// 创建并行节点。
        /// </summary>
        /// <param name="policy">汇总策略。</param>
        public BtParallel(BtParallelPolicy policy = BtParallelPolicy.RequireAll)
        {
            Policy = policy;
        }

        /// <summary>汇总策略。</summary>
        public BtParallelPolicy Policy { get; }

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            EnsureStatusBuffer();
            if (!_started)
            {
                for (var i = 0; i < Children.Count; i++)
                {
                    _statuses[i] = BtStatus.Running;
                }

                _started = true;
            }

            var anyRunning = false;
            var anySuccess = false;
            var anyFailure = false;

            for (var i = 0; i < Children.Count; i++)
            {
                if (_statuses[i] == BtStatus.Running)
                {
                    _statuses[i] = Children[i].Tick(context);
                }

                switch (_statuses[i])
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
                if (anyFailure)
                {
                    AbortRunningChildren(context);
                    Reset(context);
                    return BtStatus.Failure;
                }

                if (anyRunning)
                {
                    return BtStatus.Running;
                }

                Reset(context);
                return BtStatus.Success;
            }

            // RequireOne
            if (anySuccess)
            {
                AbortRunningChildren(context);
                Reset(context);
                return BtStatus.Success;
            }

            if (anyRunning)
            {
                return BtStatus.Running;
            }

            Reset(context);
            return BtStatus.Failure;
        }

        /// <inheritdoc />
        public override void Reset(BtContext context)
        {
            _started = false;
            if (_statuses != null)
            {
                for (var i = 0; i < _statuses.Length; i++)
                {
                    _statuses[i] = BtStatus.Running;
                }
            }

            base.Reset(context);
        }

        void EnsureStatusBuffer()
        {
            if (_statuses != null && _statuses.Length == Children.Count)
            {
                return;
            }

            _statuses = new BtStatus[Children.Count];
            for (var i = 0; i < _statuses.Length; i++)
            {
                _statuses[i] = BtStatus.Running;
            }
        }

        void AbortRunningChildren(BtContext context)
        {
            for (var i = 0; i < Children.Count; i++)
            {
                if (_statuses[i] == BtStatus.Running)
                {
                    Children[i].Abort(context);
                }
            }
        }
    }
}
