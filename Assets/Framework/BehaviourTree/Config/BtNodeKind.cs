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

        /// <summary>均匀随机选择一个子节点。</summary>
        RandomSelector = 3,

        /// <summary>按权重随机选择一个子节点。</summary>
        WeightedSelector = 4,

        /// <summary>每帧从左重评的选择器。</summary>
        ActiveSelector = 5,

        /// <summary>取反装饰。</summary>
        Inverter = 10,

        /// <summary>重复装饰。</summary>
        Repeater = 11,

        /// <summary>强制成功装饰。</summary>
        ForceSuccess = 12,

        /// <summary>强制失败装饰。</summary>
        ForceFailure = 13,

        /// <summary>直到成功装饰。</summary>
        UntilSuccess = 14,

        /// <summary>冷却装饰。</summary>
        Cooldown = 15,

        /// <summary>超时失败装饰。</summary>
        Timeout = 16,

        /// <summary>时限成功装饰。</summary>
        TimeLimit = 17,

        /// <summary>等待固定逻辑帧数。</summary>
        WaitFrames = 20,

        /// <summary>等待定点时间。</summary>
        WaitTime = 21,

        /// <summary>读取黑板布尔键。</summary>
        BlackboardBool = 22,

        /// <summary>自定义动作；TypeId 或 StringParam 为注册类型 id。</summary>
        CustomAction = 30,

        /// <summary>自定义条件；TypeId 或 StringParam 为注册类型 id。</summary>
        CustomCondition = 31,

        /// <summary>内联子树。</summary>
        Subtree = 40,
    }

    /// <summary>可序列化参数值类型。</summary>
    public enum BtParamType
    {
        /// <summary>整数。</summary>
        Int = 0,

        /// <summary>编辑器浮点（仅展示；权威时长请用 FpRaw）。</summary>
        Float = 1,

        /// <summary>布尔。</summary>
        Bool = 2,

        /// <summary>字符串。</summary>
        String = 3,

        /// <summary>定点原始 long。</summary>
        FpRaw = 4,
    }
}
