namespace Framework.BehaviourTree
{
    /// <summary>组合节点在子节点 Running 时如何重新评估兄弟/自身。</summary>
    public enum BtAbortType
    {
        /// <summary>不回头：保持当前子节点下标直到其结束。</summary>
        None = 0,

        /// <summary>每帧从左重评已成功的前缀；前缀失败则中止正在 Running 的子节点。</summary>
        Self = 1,

        /// <summary>每帧重评更高优先级（更靠左）的兄弟；其 Running/Success 则中止当前分支。</summary>
        LowerPriority = 2,

        /// <summary>同时启用 <see cref="Self"/> 与 <see cref="LowerPriority"/>。</summary>
        Both = 3,
    }
}
