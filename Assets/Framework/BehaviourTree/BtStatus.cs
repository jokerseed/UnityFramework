namespace Framework.BehaviourTree
{
    /// <summary>行为树节点在本逻辑帧的执行结果。</summary>
    public enum BtStatus
    {
        /// <summary>条件不满足或动作失败。</summary>
        Failure = 0,

        /// <summary>条件满足或动作完成。</summary>
        Success = 1,

        /// <summary>动作仍在进行，下一逻辑帧继续从该节点推进。</summary>
        Running = 2,
    }
}
