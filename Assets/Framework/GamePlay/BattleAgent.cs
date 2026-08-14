using BtTree = Framework.BehaviourTree.BehaviourTree;
using Framework.BehaviourTree;
using Framework.Core;
using Framework.FixedMath;

namespace Framework.GamePlay
{
    /// <summary>挂在 Actor 上的行为树 Agent，由 <see cref="GamePlayFramework"/> 在 Tick 中驱动。</summary>
    public sealed class BattleAgent
    {
        readonly BtTree _tree;
        readonly BtContext _context;

        /// <summary>该 Agent 追击的目标；无效则不做远距简化。</summary>
        public ActorId FocusTarget { get; }

        /// <summary>创建 Agent。</summary>
        /// <param name="tree">已编译的行为树；不可为 null。</param>
        /// <param name="blackboard">黑板；不可为 null。</param>
        /// <param name="owner">宿主对象；通常为 ActorId 或业务包装。</param>
        /// <param name="focusTarget">追击目标；远距离时只朝该目标走，不跑整棵树。</param>
        public BattleAgent(BtTree tree, BtBlackboard blackboard, object owner = null, ActorId focusTarget = default)
        {
            _tree = tree;
            _context = new BtContext(blackboard, owner, TSRandom.New(1));
            FocusTarget = focusTarget;
        }

        /// <summary>驱动一帧 AI。</summary>
        /// <param name="framework">玩法框架。</param>
        /// <param name="ownerId">该 Agent 对应的 Actor。</param>
        /// <param name="deltaTime">帧间隔（秒）。</param>
        public void Tick(GamePlayFramework framework, ActorId ownerId, float deltaTime)
        {
            _context.Owner = new BattleAiOwner(framework, ownerId);
            _context.AdvanceFrame(FP.FromFloat(deltaTime));
            _tree.Tick(_context);
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
