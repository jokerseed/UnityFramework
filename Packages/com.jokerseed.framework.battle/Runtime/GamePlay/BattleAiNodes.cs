using Framework.BehaviourTree;
using Framework.Battle;
using Framework.FixedMath;
using Framework.GAS.Abilities;
using Framework.GAS.Tags;

namespace Framework.GamePlay
{
    /// <summary>GamePlay 侧行为树自定义叶子工厂与 Agent 装配（不进入 BehaviourTree 程序集）。</summary>
    public static class BattleAiNodes
    {
        /// <summary>近战追击树资源 id（对应 <c>Assets/Bundles/BehaviourTrees/MeleeChaser.bt.json</c>）。</summary>
        public const string MeleeChaserTreeId = "MeleeChaser";

        /// <summary>近战追击的默认移动速度。</summary>
        public static readonly FP DefaultMoveSpeed = FP.FromFloat(2.2f);

        /// <summary>近战追击的默认近战距离。</summary>
        public static readonly FP DefaultMeleeRange = FP.FromFloat(2f);

        static readonly FP ArriveSlotRange = (FP)0.28f;
        static readonly FP ArriveSlotRangeSqr = ArriveSlotRange * ArriveSlotRange;

        /// <summary>
        /// 用已解析好的近战追击树模板创建 Agent（参数写入独立黑板）。
        /// 模拟层不加载资源；Unity 侧按资源 id 加载模板见 <c>Framework.GamePlay.Unity</c> 的 <c>BattleAiAssets</c>。
        /// </summary>
        /// <param name="template">近战追击树模板；不可为 null。</param>
        /// <param name="abilityId">技能 ID。</param>
        /// <param name="targetId">追击目标。</param>
        /// <param name="meleeRange">近战距离；为 null 时取 <see cref="DefaultMeleeRange"/>。</param>
        /// <param name="random">确定性随机源；为 null 时按会话种子和 Agent 的 Actor 派生，不再固定为 1。</param>
        /// <param name="agentId">该 Agent 所属 Actor；只在 <paramref name="random"/> 为 null 时参与派生。</param>
        /// <param name="sessionSeed">会话种子；只在 <paramref name="random"/> 为 null 时参与派生。</param>
        /// <returns>可交给 <see cref="GamePlayFramework.SetBattleAgent"/> 的 Agent。</returns>
        /// <exception cref="System.ArgumentNullException"><paramref name="template"/> 为 null。</exception>
        public static BattleAgent CreateMeleeChaserAgent(
            BtTreeTemplate template,
            string abilityId,
            ActorId targetId,
            FP? meleeRange = null,
            TSRandom random = null,
            ActorId agentId = default,
            int sessionSeed = 1)
        {
            if (template == null)
            {
                throw new System.ArgumentNullException(nameof(template));
            }

            BattleAiNodeRegistry.EnsureRegistered();
            var tree = new Framework.BehaviourTree.BehaviourTree(template);
            var board = new BtBlackboard();
            ApplyMeleeChaserBlackboard(board, abilityId, targetId, meleeRange ?? DefaultMeleeRange);
            return new BattleAgent(
                tree,
                board,
                focusTarget: targetId,
                random: random,
                sessionSeed: sessionSeed,
                actorValue: agentId.Value);
        }

        /// <summary>写入近战追击树运行时参数。</summary>
        /// <param name="board">黑板。</param>
        /// <param name="abilityId">技能 ID。</param>
        /// <param name="targetId">追击目标。</param>
        /// <param name="meleeRange">近战距离。</param>
        public static void ApplyMeleeChaserBlackboard(
            BtBlackboard board,
            string abilityId,
            ActorId targetId,
            FP meleeRange)
        {
            if (board == null)
            {
                return;
            }

            if (targetId.IsValid)
            {
                board.SetId(BattleAiBlackboardKeys.FocusTarget, targetId.Value);
            }

            if (!string.IsNullOrEmpty(abilityId))
            {
                board.SetString(BattleAiBlackboardKeys.AbilityId, abilityId);
            }

            board.Set(BattleAiBlackboardKeys.MeleeRange, meleeRange);
            board.Set(BattleAiBlackboardKeys.MoveSpeed, DefaultMoveSpeed);
        }

        /// <summary>条件：持有指定 Tag（支持层级前缀匹配）。</summary>
        /// <param name="tagName">标签名，如 <c>State.CrowdControl.Stunned</c>。</param>
        /// <returns>条件节点。</returns>
        public static BtCondition HasTag(string tagName) =>
            BtTreeBuilder.Condition(ctx =>
            {
                var owner = ctx.GetOwner<BattleAiOwner>();
                return owner.Framework != null &&
                       owner.Framework.TryGetActor(owner.ActorId, out var asc) &&
                       asc.Tags.HasTag(new GameplayTag(tagName));
            });

        /// <summary>条件：与目标距离不超过 range，或已站上自己的围攻槽位。</summary>
        /// <param name="targetId">目标 Actor。</param>
        /// <param name="range">距离。</param>
        /// <returns>条件节点。</returns>
        public static BtCondition InRange(ActorId targetId, FP range) =>
            BtTreeBuilder.Condition(ctx =>
            {
                var owner = ctx.GetOwner<BattleAiOwner>();
                var rangeFp = range;
                if (owner.Framework == null ||
                    !owner.Framework.Registry.TryGet(owner.ActorId, out var self) ||
                    !owner.Framework.Registry.TryGet(targetId, out var target))
                {
                    return false;
                }

                if (TSVector.Distance(self.SimPosition, target.SimPosition) <= rangeFp)
                {
                    return true;
                }

                return owner.Framework.TryGetEngagePoint(owner.ActorId, out var slot) &&
                       HorizontalDistanceSqr(self.SimPosition, slot) <= ArriveSlotRangeSqr;
            });

        /// <summary>动作：停步。</summary>
        /// <returns>动作节点。</returns>
        public static BtAction Stop() =>
            BtTreeBuilder.Action(ctx =>
            {
                var owner = ctx.GetOwner<BattleAiOwner>();
                if (owner.Framework != null)
                {
                    owner.Framework.SubmitIntent(
                        BattleIntentCommand.Move(owner.ActorId, TSVector.zero, TSVector.zero, BattleIntentSource.Ai));
                }

                return BtStatus.Success;
            });

        /// <summary>动作：朝围攻槽位（无槽则朝目标）移动，到位后成功；始终面朝目标。</summary>
        /// <param name="targetId">目标。</param>
        /// <param name="speed">速度。</param>
        /// <param name="stopRange">无槽位时的停下距离。</param>
        /// <returns>动作节点。</returns>
        public static BtAction MoveTo(ActorId targetId, FP speed, FP stopRange) =>
            BtTreeBuilder.Action(ctx =>
            {
                var owner = ctx.GetOwner<BattleAiOwner>();
                var speedFp = speed;
                var arrive = stopRange;
                if (owner.Framework == null ||
                    !owner.Framework.Registry.TryGet(owner.ActorId, out var self) ||
                    !owner.Framework.Registry.TryGet(targetId, out var target))
                {
                    return BtStatus.Failure;
                }

                var dest = target.SimPosition;
                if (owner.Framework.TryGetEngagePoint(owner.ActorId, out var slot))
                {
                    dest = slot;
                    arrive = ArriveSlotRange;
                }

                var face = target.SimPosition - self.SimPosition;
                var toDest = dest - self.SimPosition;
                toDest.y = FP.Zero;
                var distance = toDest.magnitude;
                var velocity = distance <= arrive ? TSVector.zero : toDest / distance * speedFp;
                owner.Framework.SubmitIntent(
                    BattleIntentCommand.Move(owner.ActorId, velocity, face, BattleIntentSource.Ai));
                return distance <= arrive ? BtStatus.Success : BtStatus.Running;
            });

        /// <summary>动作：尝试激活技能；冷却中本帧 Failure。</summary>
        /// <param name="abilityId">技能 ID。</param>
        /// <param name="targetId">主目标。</param>
        /// <returns>动作节点。</returns>
        public static BtAction ActivateAbility(string abilityId, ActorId targetId) =>
            BtTreeBuilder.Action(ctx =>
            {
                var owner = ctx.GetOwner<BattleAiOwner>();
                if (owner.Framework == null ||
                    !owner.Framework.Registry.TryGet(owner.ActorId, out var self) ||
                    !owner.Framework.Registry.TryGet(targetId, out var target))
                {
                    return BtStatus.Failure;
                }

                var toTarget = target.SimPosition - self.SimPosition;
                toTarget.y = FP.Zero;
                if (toTarget.sqrMagnitude < FP.EN4)
                {
                    toTarget = TSVector.forward;
                }

                var context = new AbilityActivationContext(self.SimPosition, toTarget, targetId);
                if (!owner.Framework.CanActivateAbility(owner.ActorId, abilityId, context).Success)
                {
                    return BtStatus.Failure;
                }

                owner.Framework.SubmitIntent(
                    BattleIntentCommand.Cast(
                        owner.ActorId,
                        abilityId,
                        self.SimPosition,
                        toTarget,
                        targetId,
                        BattleIntentSource.Ai));
                return BtStatus.Success;
            });

        static FP HorizontalDistanceSqr(TSVector a, TSVector b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
