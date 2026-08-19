using Framework.BehaviourTree;
using Framework.Core;
using Framework.FixedMath;
using Framework.GAS.Abilities;
using Framework.GAS.Tags;
using UnityEngine;

namespace Framework.GamePlay
{
    /// <summary>GamePlay 侧行为树自定义叶子工厂与 Agent 装配（不进入 BehaviourTree 程序集）。</summary>
    public static class BattleAiNodes
    {
        /// <summary>近战追击树资源 id（对应 <c>Assets/Bundles/BehaviourTrees/MeleeChaser.bt.json</c>）。</summary>
        public const string MeleeChaserTreeId = "MeleeChaser";

        const float DefaultMoveSpeed = 2.2f;
        const float DefaultMeleeRange = 2f;
        const float ArriveSlotRange = 0.28f;

        /// <summary>
        /// 从热更资源加载近战追击树拓扑（参数请用 <see cref="ApplyMeleeChaserBlackboard"/> 写入 Agent 黑板）。
        /// </summary>
        /// <returns>独立 Runtime 的行为树实例。</returns>
        public static Framework.BehaviourTree.BehaviourTree CreateMeleeChaser()
        {
            BattleAiNodeRegistry.EnsureRegistered();
            return BtTreeResource.LoadTree(MeleeChaserTreeId);
        }

        /// <summary>创建近战追击 Agent（资源树 + 独立黑板）。</summary>
        /// <param name="abilityId">技能 ID。</param>
        /// <param name="targetId">追击目标。</param>
        /// <param name="meleeRange">近战距离；默认 2。</param>
        /// <param name="random">确定性随机源；为 null 时使用种子 1。</param>
        /// <returns>可交给 <see cref="GamePlayFramework.SetBattleAgent"/> 的 Agent。</returns>
        public static BattleAgent CreateMeleeChaserAgent(
            string abilityId,
            ActorId targetId,
            float meleeRange = DefaultMeleeRange,
            TSRandom random = null)
        {
            BattleAiNodeRegistry.EnsureRegistered();
            var tree = BtTreeResource.LoadTree(MeleeChaserTreeId);
            var board = new BtBlackboard();
            ApplyMeleeChaserBlackboard(board, abilityId, targetId, meleeRange);
            return new BattleAgent(tree, board, focusTarget: targetId, random: random);
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
            float meleeRange = DefaultMeleeRange)
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
                board.SetObject(BattleAiBlackboardKeys.AbilityId, abilityId);
            }

            board.Set(BattleAiBlackboardKeys.MeleeRange, FP.FromFloat(meleeRange));
            board.Set(BattleAiBlackboardKeys.MoveSpeed, FP.FromFloat(DefaultMoveSpeed));
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
        public static BtCondition InRange(ActorId targetId, float range) =>
            BtTreeBuilder.Condition(ctx =>
            {
                var owner = ctx.GetOwner<BattleAiOwner>();
                if (owner.Framework == null ||
                    !owner.Framework.Registry.TryGet(owner.ActorId, out var self) ||
                    !owner.Framework.Registry.TryGet(targetId, out var target))
                {
                    return false;
                }

                if (Vector3.Distance(self.Position, target.Position) <= range)
                {
                    return true;
                }

                return owner.Framework.TryGetEngagePoint(owner.ActorId, out var slot) &&
                       HorizontalDistanceSqr(self.Position, slot) <= ArriveSlotRange * ArriveSlotRange;
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
                        BattleIntentCommand.Move(owner.ActorId, Vector3.zero, Vector3.zero, BattleIntentSource.Ai));
                }

                return BtStatus.Success;
            });

        /// <summary>动作：朝围攻槽位（无槽则朝目标）移动，到位后成功；始终面朝目标。</summary>
        /// <param name="targetId">目标。</param>
        /// <param name="speed">速度。</param>
        /// <param name="stopRange">无槽位时的停下距离。</param>
        /// <returns>动作节点。</returns>
        public static BtAction MoveTo(ActorId targetId, float speed, float stopRange) =>
            BtTreeBuilder.Action(ctx =>
            {
                var owner = ctx.GetOwner<BattleAiOwner>();
                if (owner.Framework == null ||
                    !owner.Framework.Registry.TryGet(owner.ActorId, out var self) ||
                    !owner.Framework.Registry.TryGet(targetId, out var target))
                {
                    return BtStatus.Failure;
                }

                var dest = target.Position;
                var arrive = stopRange;
                if (owner.Framework.TryGetEngagePoint(owner.ActorId, out var slot))
                {
                    dest = slot;
                    arrive = ArriveSlotRange;
                }

                var face = target.Position - self.Position;
                var toDest = dest - self.Position;
                toDest.y = 0f;
                var distance = toDest.magnitude;
                var velocity = distance <= arrive ? Vector3.zero : toDest / distance * speed;
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

                var toTarget = target.Position - self.Position;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude < 0.0001f)
                {
                    toTarget = Vector3.forward;
                }

                var context = new AbilityActivationContext(self.Position, toTarget, targetId);
                if (!owner.Framework.CanActivateAbility(owner.ActorId, abilityId, context).Success)
                {
                    return BtStatus.Failure;
                }

                owner.Framework.SubmitIntent(
                    BattleIntentCommand.Cast(
                        owner.ActorId,
                        abilityId,
                        self.Position,
                        toTarget,
                        targetId,
                        BattleIntentSource.Ai));
                return BtStatus.Success;
            });

        static float HorizontalDistanceSqr(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
