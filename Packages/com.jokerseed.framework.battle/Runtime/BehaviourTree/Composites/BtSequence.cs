namespace Framework.BehaviourTree
{
    /// <summary>
    /// 顺序节点：从左到右；任一失败则失败，全部成功则成功。
    /// <see cref="BtAbortType.Self"/> / <see cref="BtAbortType.Both"/> 时每帧从左重评前缀。
    /// </summary>
    public sealed class BtSequence : BtComposite
    {
        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            var runtime = context.Runtime;
            var current = runtime != null ? runtime.GetInt(Index) : 0;
            var reactive = AbortType == BtAbortType.Self || AbortType == BtAbortType.Both;

            if (reactive && current > 0)
            {
                for (var i = 0; i < current; i++)
                {
                    var prefix = Children[i].Tick(context);
                    if (prefix == BtStatus.Running)
                    {
                        AbortChildrenFrom(context, i + 1);
                        runtime?.SetInt(Index, i);
                        return Commit(context, BtStatus.Running);
                    }

                    if (prefix == BtStatus.Failure)
                    {
                        AbortChild(context, current);
                        Reset(context);
                        return Commit(context, BtStatus.Failure);
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

                if (status == BtStatus.Failure)
                {
                    Reset(context);
                    return Commit(context, BtStatus.Failure);
                }

                current++;
                runtime?.SetInt(Index, current);
            }

            Reset(context);
            return Commit(context, BtStatus.Success);
        }
    }
}
