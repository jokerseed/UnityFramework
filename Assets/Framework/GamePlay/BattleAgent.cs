using System;
using BtTree = Framework.BehaviourTree.BehaviourTree;
using Framework.BehaviourTree;
using Framework.Core;
using Framework.FixedMath;

namespace Framework.GamePlay
{
    /// <summary>挂在 Actor 上的行为树 Agent，由 <see cref="GamePlayFramework"/> 在 Tick 中驱动。</summary>
    public sealed class BattleAgent
    {
        BtTree _tree;
        readonly BtContext _context;

        /// <summary>该 Agent 追击的目标；无效则不做远距简化。</summary>
        public ActorId FocusTarget { get; }

        /// <summary>当前行为树实例。</summary>
        public BtTree Tree => _tree;

        /// <summary>黑板（替换树时可选择保留）。</summary>
        public BtBlackboard Blackboard => _context.Blackboard;

        /// <summary>创建 Agent。</summary>
        /// <param name="tree">已编译的行为树；不可为 null。</param>
        /// <param name="blackboard">黑板；不可为 null。</param>
        /// <param name="owner">宿主对象；通常为 ActorId 或业务包装。</param>
        /// <param name="focusTarget">追击目标；远距离时只朝该目标走，不跑整棵树。</param>
        /// <param name="random">确定性随机源；为 null 时使用种子 1 的独立实例。</param>
        /// <exception cref="ArgumentNullException"><paramref name="tree"/> 或 <paramref name="blackboard"/> 为 null。</exception>
        public BattleAgent(
            BtTree tree,
            BtBlackboard blackboard,
            object owner = null,
            ActorId focusTarget = default,
            TSRandom random = null)
        {
            _tree = tree ?? throw new ArgumentNullException(nameof(tree));
            if (blackboard == null)
            {
                throw new ArgumentNullException(nameof(blackboard));
            }

            _context = new BtContext(blackboard, owner, random ?? TSRandom.New(1));
            FocusTarget = focusTarget;
        }

        /// <summary>驱动一帧 AI。</summary>
        /// <param name="framework">玩法框架。</param>
        /// <param name="ownerId">该 Agent 对应的 Actor。</param>
        /// <param name="deltaTime">帧间隔（秒，定点）。</param>
        public void Tick(GamePlayFramework framework, ActorId ownerId, FP deltaTime)
        {
            _context.Owner = new BattleAiOwner(framework, ownerId);
            _context.AdvanceFrame(deltaTime);
            _tree.Tick(_context);
        }

        /// <summary>
        /// 热替换行为树：中止旧树后换上新实例；默认保留黑板，Runtime 从干净槽开始。
        /// </summary>
        /// <param name="newTree">新树；不可为 null。</param>
        /// <param name="clearBlackboard">为 true 时清空权威黑板。</param>
        /// <exception cref="ArgumentNullException"><paramref name="newTree"/> 为 null。</exception>
        public void ReplaceTree(BtTree newTree, bool clearBlackboard = false)
        {
            if (newTree == null)
            {
                throw new ArgumentNullException(nameof(newTree));
            }

            _tree.Abort(_context);
            _tree = newTree;
            if (clearBlackboard)
            {
                _context.Blackboard.Clear();
            }
        }

        /// <summary>从热更资源加载并替换当前树。</summary>
        /// <param name="treeId">树 id（与 Bundles 中资产名一致）。</param>
        /// <param name="clearBlackboard">是否清空黑板。</param>
        /// <param name="customRegistry">自定义节点注册表；可为 null。</param>
        /// <param name="subtrees">子树解析；可为 null。</param>
        public void ReplaceFromResource(
            string treeId,
            bool clearBlackboard = false,
            IBtNodeRegistry customRegistry = null,
            IBtSubtreeResolver subtrees = null)
        {
            var tree = BtTreeResource.LoadTree(treeId, customRegistry, subtrees);
            ReplaceTree(tree, clearBlackboard);
        }
    }

    /// <summary>行为树自定义节点使用的宿主包装。</summary>
    public sealed class BattleAiOwner
    {
        /// <summary>玩法框架。</summary>
        public GamePlayFramework Framework { get; }

        /// <summary>该树所属 Actor。</summary>
        public ActorId ActorId { get; }

        /// <summary>构造宿主包装。</summary>
        /// <param name="framework">玩法框架。</param>
        /// <param name="actorId">所属 Actor。</param>
        public BattleAiOwner(GamePlayFramework framework, ActorId actorId)
        {
            Framework = framework;
            ActorId = actorId;
        }
    }
}
