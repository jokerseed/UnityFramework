using System.Collections.Generic;
using Framework.Core;
using Framework.GAS.Tags;
using UnityEngine;

namespace Framework.GAS.Abilities
{
    /// <summary>
    /// 技能基类，定义冷却、前提标签检查及激活接口。
    /// 具体技能继承此类并实现 <see cref="Activate"/>。
    /// </summary>
    public abstract class GameplayAbility
    {
        /// <summary>技能唯一标识符。</summary>
        public string AbilityId { get; }

        /// <summary>技能冷却时间（秒）；激活后 ASC 将启动对应冷却计时。</summary>
        public float Cooldown { get; }

        /// <summary>激活所需的 GameplayTag 列表；拥有者必须持有所有标签方可激活。</summary>
        public IReadOnlyList<GameplayTag> RequiredTags { get; }

        /// <summary>阻止激活的 GameplayTag 列表；拥有者持有任意标签时不可激活。</summary>
        public IReadOnlyList<GameplayTag> BlockedTags { get; }

        /// <summary>初始化技能基础属性。</summary>
        /// <param name="abilityId">技能唯一 ID；不可为 null 或空。</param>
        /// <param name="cooldown">冷却时间（秒）；0 表示无冷却。</param>
        /// <param name="requiredTags">激活前提标签列表；为 null 时视为空列表。</param>
        /// <param name="blockedTags">阻止激活的标签列表；为 null 时视为空列表。</param>
        protected GameplayAbility(
            string abilityId,
            float cooldown,
            IReadOnlyList<GameplayTag> requiredTags = null,
            IReadOnlyList<GameplayTag> blockedTags = null)
        {
            AbilityId = abilityId;
            Cooldown = cooldown;
            RequiredTags = requiredTags ?? System.Array.Empty<GameplayTag>();
            BlockedTags = blockedTags ?? System.Array.Empty<GameplayTag>();
        }

        /// <summary>检查技能是否满足激活条件（冷却、标签前提）。子类可重写以添加自定义检查。</summary>
        /// <param name="owner">持有该技能的 ASC。</param>
        /// <param name="context">激活上下文。</param>
        /// <returns>检查结果；成功则可调用 <see cref="Activate"/>。</returns>
        public virtual AbilityActivationResult CanActivate(AbilitySystemComponent owner, in AbilityActivationContext context)
        {
            if (owner.CooldownRemaining(AbilityId) > 0f)
            {
                return AbilityActivationResult.Failed(AbilityActivationFailureReason.OnCooldown);
            }

            if (!owner.Tags.HasAll(RequiredTags))
            {
                return AbilityActivationResult.Failed(AbilityActivationFailureReason.MissingRequiredTags);
            }

            if (owner.Tags.HasAny(BlockedTags))
            {
                return AbilityActivationResult.Failed(AbilityActivationFailureReason.HasBlockingTags);
            }

            return AbilityActivationResult.Succeeded();
        }

        /// <summary>执行技能激活逻辑（写入命令缓冲 / 发布表现事件）。</summary>
        /// <param name="owner">持有该技能的 ASC。</param>
        /// <param name="context">激活上下文（起点、方向、目标、范围）。</param>
        /// <param name="battle">当前帧战斗上下文，用于写入 CommandBuffer 或发布事件。</param>
        public abstract void Activate(AbilitySystemComponent owner, in AbilityActivationContext context, BattleContext battle);

        /// <summary>技能结束时的清理回调；默认无操作，子类可重写。</summary>
        /// <param name="owner">持有该技能的 ASC。</param>
        public virtual void End(AbilitySystemComponent owner) { }
    }

    /// <summary>技能激活上下文，描述激活瞬间的空间信息与目标。</summary>
    public readonly struct AbilityActivationContext
    {
        /// <summary>技能发射/施放起点（世界坐标）。</summary>
        public Vector3 Origin { get; }

        /// <summary>施放方向（已归一化）；若传入零向量则默认为 <see cref="Vector3.forward"/>。</summary>
        public Vector3 Direction { get; }

        /// <summary>主目标单位 ID；无目标时为默认值（<see cref="ActorId.IsValid"/> 为 false）。</summary>
        public ActorId PrimaryTarget { get; }

        /// <summary>技能有效范围（米）；0 表示由技能自身定义范围。</summary>
        public float Range { get; }

        /// <summary>构造技能激活上下文。</summary>
        /// <param name="origin">施放起点（世界坐标）。</param>
        /// <param name="direction">施放方向；零向量时自动设为 <see cref="Vector3.forward"/>。</param>
        /// <param name="primaryTarget">主目标 ID；无目标传 default。</param>
        /// <param name="range">有效范围（米）；0 表示由技能自定义。</param>
        public AbilityActivationContext(
            Vector3 origin,
            Vector3 direction,
            ActorId primaryTarget = default,
            float range = 0f)
        {
            Origin = origin;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
            PrimaryTarget = primaryTarget;
            Range = range;
        }
    }
}
