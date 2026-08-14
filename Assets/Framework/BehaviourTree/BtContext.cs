using System;
using Framework.FixedMath;

namespace Framework.BehaviourTree
{
    /// <summary>
    /// 单次行为树 Tick 的上下文。必须由逻辑帧驱动，禁止填入 <c>Time.deltaTime</c>。
    /// </summary>
    public sealed class BtContext
    {
        /// <summary>
        /// 创建上下文。
        /// </summary>
        /// <param name="blackboard">黑板；不可为 null。</param>
        /// <param name="owner">宿主对象；可为 null。优先实现 <see cref="IBtAgent"/> 并赋给 <see cref="Agent"/>。</param>
        /// <param name="random">确定性随机；可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="blackboard"/> 为 null。</exception>
        public BtContext(BtBlackboard blackboard, object owner = null, TSRandom random = null)
        {
            Blackboard = blackboard ?? throw new ArgumentNullException(nameof(blackboard));
            Owner = owner;
            Agent = owner as IBtAgent;
            Random = random;
        }

        /// <summary>当前逻辑帧序号（由外部推进）。</summary>
        public int FrameIndex { get; set; }

        /// <summary>本逻辑帧固定步长。</summary>
        public FP DeltaTime { get; set; }

        /// <summary>黑板。</summary>
        public BtBlackboard Blackboard { get; }

        /// <summary>宿主对象；业务节点可继续 <see cref="GetOwner{T}"/>。</summary>
        public object Owner { get; set; }

        /// <summary>类型化宿主；与 <see cref="Owner"/> 可指向同一实例。</summary>
        public IBtAgent Agent { get; set; }

        /// <summary>确定性随机源；须用 <see cref="TSRandom.New"/> 创建。</summary>
        public TSRandom Random { get; set; }

        /// <summary>当前 Agent 的运行时槽；由 <see cref="BehaviourTree.Tick"/> 注入。</summary>
        public BtRuntime Runtime { get; set; }

        /// <summary>为 true 时采集调试帧（Stopwatch）；权威逻辑应保持 false。</summary>
        public bool CollectDebug { get; set; }

        /// <summary>本帧是否命中断点。</summary>
        public bool BreakpointHit { get; set; }

        /// <summary>命中断点的节点下标；无则为 -1。</summary>
        public int BreakpointNodeIndex { get; set; } = -1;

        /// <summary>推进到下一逻辑帧。</summary>
        /// <param name="deltaTime">固定步长。</param>
        public void AdvanceFrame(FP deltaTime)
        {
            FrameIndex++;
            DeltaTime = deltaTime;
            BreakpointHit = false;
            BreakpointNodeIndex = -1;
        }

        /// <summary>按类型获取宿主。</summary>
        /// <typeparam name="T">宿主类型。</typeparam>
        /// <returns>转型后的宿主；失败则为 null。</returns>
        public T GetOwner<T>() where T : class => Owner as T;

        /// <summary>按类型获取 <see cref="Agent"/>。</summary>
        /// <typeparam name="T">宿主类型。</typeparam>
        /// <returns>转型后的 Agent；失败则为 null。</returns>
        public T GetAgent<T>() where T : class, IBtAgent => Agent as T;
    }
}
