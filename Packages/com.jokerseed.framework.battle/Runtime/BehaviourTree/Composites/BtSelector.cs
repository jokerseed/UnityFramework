namespace Framework.BehaviourTree
{
    /// <summary>
    /// 选择节点：从左到右；任一成功则成功，全部失败则失败。
    /// <see cref="BtAbortType.LowerPriority"/> / <see cref="BtAbortType.Both"/> 时每帧重评更高优先级兄弟。
    /// </summary>
    public class BtSelector : BtComposite
    {
        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            var runtime = context.Runtime;
            var current = runtime != null ? runtime.GetInt(Index) : 0;
            var preempt = AbortType == BtAbortType.LowerPriority || AbortType == BtAbortType.Both;

            if (preempt && current > 0)
            {
                for (var i = 0; i < current; i++)
                {
                    var higher = Children[i].Tick(context);
                    if (higher == BtStatus.Running || higher == BtStatus.Success)
                    {
                        AbortChild(context, current);
                        if (higher == BtStatus.Success)
                        {
                            Reset(context);
                            return Commit(context, BtStatus.Success);
                        }

                        runtime?.SetInt(Index, i);
                        return Commit(context, BtStatus.Running);
                    }

                    Children[i].Reset(context);
                }
            }

            while (current < Children.Count)
            {
                var status = Children[current].Tick(context);
                if (status == BtStatus.Running)
                {
                    runtime?.SetInt(Index, current);
                    return Commit(context, BtStatus.Running);
                }

                if (status == BtStatus.Success)
                {
                    Reset(context);
                    return Commit(context, BtStatus.Success);
                }

                current++;
                runtime?.SetInt(Index, current);
            }

            Reset(context);
            return Commit(context, BtStatus.Failure);
        }
    }
}
