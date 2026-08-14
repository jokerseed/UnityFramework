namespace Framework.BehaviourTree
{
    /// <summary>每帧从左重评的选择器（LowerPriority 打断）。</summary>
    public sealed class BtActiveSelector : BtSelector
    {
        /// <summary>创建主动选择器。</summary>
        public BtActiveSelector()
        {
            AbortType = BtAbortType.LowerPriority;
        }
    }

    /// <summary>激活时按均匀随机挑一个子节点执行，直到该子节点结束。</summary>
    public sealed class BtRandomSelector : BtComposite
    {
        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            var runtime = context.Runtime;
            var picked = runtime != null && runtime.IsStarted(Index)
                ? runtime.GetInt(Index)
                : Pick(context);

            if (runtime != null)
            {
                runtime.SetStarted(Index, true);
                runtime.SetInt(Index, picked);
            }

            if (picked < 0 || picked >= Children.Count)
            {
                Reset(context);
                return Commit(context, BtStatus.Failure);
            }

            var status = Children[picked].Tick(context);
            if (status == BtStatus.Running)
            {
                return Commit(context, BtStatus.Running);
            }

            Reset(context);
            return Commit(context, status);
        }

        int Pick(BtContext context)
        {
            if (Children.Count <= 0)
            {
                return -1;
            }

            if (context.Random == null)
            {
                return 0;
            }

            return context.Random.Next(0, Children.Count);
        }
    }

    /// <summary>激活时按权重随机挑一个子节点执行，直到该子节点结束。</summary>
    public sealed class BtWeightedSelector : BtComposite
    {
        readonly int[] _weights;

        /// <summary>创建加权选择器。</summary>
        /// <param name="weights">与子节点对应的权重；null 或短于子节点时缺省为 1。</param>
        public BtWeightedSelector(int[] weights = null)
        {
            _weights = weights;
        }

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            var runtime = context.Runtime;
            var picked = runtime != null && runtime.IsStarted(Index)
                ? runtime.GetInt(Index)
                : Pick(context);

            if (runtime != null)
            {
                runtime.SetStarted(Index, true);
                runtime.SetInt(Index, picked);
            }

            if (picked < 0 || picked >= Children.Count)
            {
                Reset(context);
                return Commit(context, BtStatus.Failure);
            }

            var status = Children[picked].Tick(context);
            if (status == BtStatus.Running)
            {
                return Commit(context, BtStatus.Running);
            }

            Reset(context);
            return Commit(context, status);
        }

        int Pick(BtContext context)
        {
            if (Children.Count <= 0)
            {
                return -1;
            }

            var total = 0;
            for (var i = 0; i < Children.Count; i++)
            {
                total += WeightAt(i);
            }

            if (total <= 0)
            {
                return 0;
            }

            var roll = context.Random != null ? context.Random.Next(0, total) : 0;
            var acc = 0;
            for (var i = 0; i < Children.Count; i++)
            {
                acc += WeightAt(i);
                if (roll < acc)
                {
                    return i;
                }
            }

            return Children.Count - 1;
        }

        int WeightAt(int index)
        {
            if (_weights == null || index >= _weights.Length)
            {
                return 1;
            }

            var w = _weights[index];
            return w > 0 ? w : 0;
        }
    }
}
