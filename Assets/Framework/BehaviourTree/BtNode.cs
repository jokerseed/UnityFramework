using System.Collections.Generic;
using Framework.FixedMath;

namespace Framework.BehaviourTree
{
    /// <summary>
    /// 行为树节点。拓扑可被多个 Agent 共享；Running 进度必须写在 <see cref="BtRuntime"/>。
    /// </summary>
    public abstract class BtNode
    {
        /// <summary>模板内下标；绑定后 >= 0。</summary>
        public int Index { get; internal set; } = -1;

        /// <summary>节点调试名；可为 null。</summary>
        public string Name { get; set; }

        /// <summary>工厂/自定义类型 id；代码闭包叶子可为 null。</summary>
        public string TypeId { get; set; }

        /// <summary>来自配置的节点 id，供调试着色；代码树可为 null。</summary>
        public string ConfigId { get; set; }

        /// <summary>条件打断类型；叶子忽略。默认 <see cref="BtAbortType.None"/>。</summary>
        public BtAbortType AbortType { get; set; }

        /// <summary>为 true 时 Tick 该节点后置位 <see cref="BtContext.BreakpointHit"/>。</summary>
        public bool Breakpoint { get; set; }

        /// <summary>直接子节点数量。</summary>
        public virtual int ChildCount => 0;

        /// <summary>取直接子节点。</summary>
        /// <param name="index">子下标。</param>
        /// <returns>子节点；越界为 null。</returns>
        public virtual BtNode GetChild(int index) => null;

        /// <summary>在本逻辑帧执行节点。</summary>
        /// <param name="context">Tick 上下文；不可为 null。</param>
        /// <returns>本帧执行状态。</returns>
        public abstract BtStatus Tick(BtContext context);

        /// <summary>重置该节点在 Runtime 中的槽位，并递归 Reset 子节点。</summary>
        /// <param name="context">Tick 上下文；不可为 null。</param>
        public virtual void Reset(BtContext context)
        {
            ClearSlot(context);
        }

        /// <summary>中止当前 Running 分支；必须先通知叶子收尾再 Reset。</summary>
        /// <param name="context">Tick 上下文；不可为 null。</param>
        public virtual void Abort(BtContext context)
        {
            Reset(context);
        }

        /// <summary>将本帧状态写入运行时并处理断点。</summary>
        /// <param name="context">上下文。</param>
        /// <param name="status">状态。</param>
        /// <returns><paramref name="status"/>。</returns>
        protected BtStatus Commit(BtContext context, BtStatus status)
        {
            if (context?.Runtime != null && Index >= 0)
            {
                context.Runtime.SetStatus(Index, status);
            }

            if (Breakpoint && context != null)
            {
                context.BreakpointHit = true;
                context.BreakpointNodeIndex = Index;
            }

            return status;
        }

        /// <summary>清空本节点运行时槽。</summary>
        /// <param name="context">上下文。</param>
        protected void ClearSlot(BtContext context)
        {
            var runtime = context?.Runtime;
            if (runtime == null || Index < 0)
            {
                return;
            }

            runtime.SetInt(Index, 0);
            runtime.SetFp(Index, FP.Zero);
            runtime.SetStarted(Index, false);
            runtime.SetStatus(Index, BtStatus.Failure);
        }

        internal static BtNode[] Flatten(BtNode root)
        {
            var list = new List<BtNode>(32);
            Walk(root, list);
            for (var i = 0; i < list.Count; i++)
            {
                list[i].Index = i;
            }

            return list.ToArray();
        }

        static void Walk(BtNode node, List<BtNode> list)
        {
            if (node == null)
            {
                return;
            }

            list.Add(node);
            var count = node.ChildCount;
            for (var i = 0; i < count; i++)
            {
                Walk(node.GetChild(i), list);
            }
        }
    }
}
