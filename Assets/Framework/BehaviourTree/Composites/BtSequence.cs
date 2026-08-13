namespace Framework.BehaviourTree
{
    /// <summary>
    /// 顺序节点：从左到右执行；任一失败则失败，全部成功则成功；子节点 Running 时保持进度。
    /// </summary>
    public sealed class BtSequence : BtComposite
    {
        int _currentIndex;

        /// <inheritdoc />
        public override BtStatus Tick(BtContext context)
        {
            while (_currentIndex < Children.Count)
            {
                var status = Children[_currentIndex].Tick(context);
                if (status == BtStatus.Running)
                {
                    return BtStatus.Running;
                }

                if (status == BtStatus.Failure)
                {
                    Reset(context);
                    return BtStatus.Failure;
                }

                _currentIndex++;
            }

            Reset(context);
            return BtStatus.Success;
        }

        /// <inheritdoc />
        public override void Reset(BtContext context)
        {
            _currentIndex = 0;
            base.Reset(context);
        }
    }
}
