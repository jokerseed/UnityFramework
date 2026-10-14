namespace Framework.BehaviourTree
{
    /// <summary>单帧调试采样（非权威，禁止写入锁步判定）。</summary>
    public sealed class BtDebugFrame
    {
        /// <summary>按节点下标的本帧状态。</summary>
        public BtStatus[] Statuses { get; set; }

        /// <summary>从根到当前 Running 叶子的下标路径；无 Running 则为空。</summary>
        public int[] RunningPath { get; set; }

        /// <summary>本帧 Tick 耗时（Stopwatch ticks）；未采集则为 0。</summary>
        public long ElapsedTicks { get; set; }

        /// <summary>触发断点的节点下标；无则为 -1。</summary>
        public int BreakpointNodeIndex { get; set; } = -1;
    }

    /// <summary>运行时把调试帧交给编辑器/工具。</summary>
    public static class BtDebugHub
    {
        /// <summary>最近一次被发布的树；可能为 null。</summary>
        public static BehaviourTree LastTree { get; private set; }

        /// <summary>最近一次上下文；可能为 null。</summary>
        public static BtContext LastContext { get; private set; }

        /// <summary>最近一次调试帧；可能为 null。</summary>
        public static BtDebugFrame LastFrame { get; private set; }

        /// <summary>仅匹配该树名时覆盖 Last*；null 表示接受任意树。</summary>
        public static string FilterName { get; set; }

        /// <summary>发布一帧调试数据。</summary>
        /// <param name="tree">树；不可为 null。</param>
        /// <param name="context">上下文；不可为 null。</param>
        /// <param name="frame">调试帧；不可为 null。</param>
        public static void Publish(BehaviourTree tree, BtContext context, BtDebugFrame frame)
        {
            if (tree == null || context == null || frame == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(FilterName) && tree.Name != FilterName)
            {
                return;
            }

            LastTree = tree;
            LastContext = context;
            LastFrame = frame;
        }

        /// <summary>清空监视。</summary>
        public static void Clear()
        {
            LastTree = null;
            LastContext = null;
            LastFrame = null;
        }
    }
}
