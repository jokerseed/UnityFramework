using Framework.BehaviourTree;
using Framework.Core;
using Framework.GAS.Abilities;
using Framework.GAS.Tags;
using UnityEngine;

namespace Framework.GamePlay
{
    /// <summary>GamePlay 侧行为树自定义叶子工厂（不进入 BehaviourTree 程序集）。</summary>
    public static class BattleAiNodes
    {
        const float DefaultMoveSpeed = 2.2f;
        const float DefaultMeleeRange = 2f;
        const float ArriveSlotRange = 0.28f;

        /// <summary>构建默认近战怪物树：存活则靠近并释放指定技能。</summary>
        /// <param name="abilityId">近战技能 ID。</param>
        /// <param name="targetId">追击目标。</param>
        /// <param name="meleeRange">近战距离；默认 1.9。</param>
        /// <returns>行为树实例。</returns>
        public static Framework.BehaviourTree.BehaviourTree CreateMeleeChaser(
            string abilityId,
            ActorId targetId,
            float meleeRange = DefaultMeleeRange)
        {
            var combat = BtTreeBuilder.Selector()
                .AddChild(
                    BtTreeBuilder.Sequence()
                        .AddChild(InRange(targetId, meleeRange))
                        .AddChild(Stop())
                        .AddChild(ActivateAbility(abilityId, targetId)))
                .AddChild(MoveTo(targetId, DefaultMoveSpeed, meleeRange * 0.85f));

            var root = BtTreeBuilder.Sequence()
                .AddChild(BtTreeBuilder.Condition(IsAlive))
                .AddChild(combat);

            return new BtTreeBuilder().Root(root).Build("MeleeChaser");
        }

        /// <summary>创建近战追击 Agent（含独立黑板）。</summary>
        /// <param name="abilityId">技能 ID。</param>
        /// <param name="targetId">追击目标。</param>
        /// <param name="meleeRange">近战距离；默认 1.9。</param>
        /// <returns>可交给 <see cref="GamePlayFramework.SetBattleAgent"/> 的 Agent。</returns>
        public static BattleAgent CreateMeleeChaserAgent(
            string abilityId,
            ActorId targetId,
            float meleeRange = DefaultMeleeRange)
        {
            return new BattleAgent(CreateMeleeChaser(abilityId, targetId, meleeRange), new BtBlackboard(), focusTarget: targetId);
        }

        static bool IsAlive(BtContext ctx)
        {
            var owner = ctx.GetOwner<BattleAiOwner>();
            return owner.Framework != null &&
                   owner.Framework.TryGetActor(owner.ActorId, out var asc) &&
                   !asc.IsDead &&
                   !asc.Tags.HasTag(new GameplayTag(BattleConstants.TagStunned)) &&
                   !asc.Tags.HasTag(new GameplayTag(BattleConstants.TagKnockedDown));
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
                owner.Framework?.SetActorVelocity(owner.ActorId, Vector3.zero);
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

                FaceToward(owner, target.Position - self.Position);

                var toDest = dest - self.Position;
                toDest.y = 0f;
                var distance = toDest.magnitude;
                if (distance <= arrive)
                {
                    owner.Framework.SetActorVelocity(owner.ActorId, Vector3.zero);
                    return BtStatus.Success;
                }

                owner.Framework.SetActorVelocity(owner.ActorId, toDest / distance * speed);
                return BtStatus.Running;
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
                var result = owner.Framework.TryActivateAbility(owner.ActorId, abilityId, context);
                return result.Success ? BtStatus.Success : BtStatus.Failure;
            });

        static void FaceToward(BattleAiOwner owner, Vector3 toTarget)
        {
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return;
            }

            owner.Framework.Registry.SetForward(owner.ActorId, toTarget.normalized);
        }

        static float HorizontalDistanceSqr(Vector3 a, Vector3 b)
        {
            var dx = a.x - b.x;
            var dz = a.z - b.z;
            return dx * dx + dz * dz;
        }
    }
}
