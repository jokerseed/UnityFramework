namespace Framework.BehaviourTree
{
    /// <summary>配置文件中行为树节点类型标识。</summary>
    public enum BtNodeKind
    {
        /// <summary>顺序组合。</summary>
        Sequence = 0,

        /// <summary>选择组合。</summary>
        Selector = 1,

        /// <summary>并行组合。</summary>
        Parallel = 2,

        /// <summary>取反装饰。</summary>
        Inverter = 10,

        /// <summary>重复装饰。</summary>
        Repeater = 11,

        /// <summary>强制成功装饰。</summary>
        ForceSuccess = 12,

        /// <summary>等待固定逻辑帧数。</summary>
        WaitFrames = 20,

        /// <summary>等待定点时间（秒，编译为 FP）。</summary>
        WaitTime = 21,

        /// <summary>读取黑板布尔键；键存在且为 true 时成功。</summary>
        BlackboardBool = 22,

        /// <summary>自定义动作；<see cref="BtConfigNode.StringParam"/> 为注册类型 id。</summary>
        CustomAction = 30,

        /// <summary>自定义条件；<see cref="BtConfigNode.StringParam"/> 为注册类型 id。</summary>
        CustomCondition = 31,
    }
}
