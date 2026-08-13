namespace Framework.BehaviourTree
{
    /// <summary>
    /// 选择节点：从左到右执行；任一成功则成功，全部失败则失败；子节点 Running 时保持进度。
    /// </summary>
    public sealed class BtSelector : BtComposite
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

                if (status == BtStatus.Success)
                {
                    Reset(context);
                    return BtStatus.Success;
                }

                _currentIndex++;
            }

            Reset(context);
            return BtStatus.Failure;
        }

        /// <inheritdoc />
        public override void Reset(BtContext context)
        {
            _currentIndex = 0;
            base.Reset(context);
        }
    }
}
