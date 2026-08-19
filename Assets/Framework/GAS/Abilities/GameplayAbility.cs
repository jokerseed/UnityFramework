using System.Collections.Generic;
using Framework.Core;
using Framework.FixedMath;
using Framework.GAS.Tags;
using UnityEngine;

namespace Framework.GAS.Abilities
{
    /// <summary>
    /// 技能基类，定义冷却、前提标签检查及激活接口。
    /// 激活流程：<see cref="CanActivate"/> → <see cref="Commit"/> → <see cref="Activate"/> → <see cref="End"/>。
    /// </summary>
    public abstract class GameplayAbility
    {
        /// <summary>技能唯一标识符。</summary>
        public string AbilityId { get; }

        /// <summary>技能冷却时间（秒）；Commit 后 ASC 将启动对应冷却计时。</summary>
        public FP Cooldown { get; }

        /// <summary>激活所需的 GameplayTag 列表；拥有者必须持有所有标签方可激活。</summary>
        public IReadOnlyList<GameplayTag> RequiredTags { get; }

        /// <summary>阻止激活的 GameplayTag 列表；拥有者持有任意标签时不可激活。</summary>
        public IReadOnlyList<GameplayTag> BlockedTags { get; }

        /// <summary>冷却键；空则使用 <see cref="AbilityId"/>，用于共享冷却组。</summary>
        public virtual string CooldownId => null;

        /// <summary>技能身份 Tag。</summary>
        public virtual IReadOnlyList<GameplayTag> AssetTags => System.Array.Empty<GameplayTag>();

        /// <summary>激活期间授予拥有者的 Tag。</summary>
        public virtual IReadOnlyList<GameplayTag> ActivationOwnedTags => System.Array.Empty<GameplayTag>();

        /// <summary>激活时取消其它匹配技能所用的 Tag。</summary>
        public virtual IReadOnlyList<GameplayTag> CancelAbilitiesWithTags => System.Array.Empty<GameplayTag>();

        /// <summary>激活时是否立即 Commit（设冷却）；false 时由子类手动调用 Commit。</summary>
        public virtual bool AutoCommit { get; } = true;

        /// <summary>激活属性消耗（属性名 → 量）；默认无消耗。</summary>
        public virtual IReadOnlyDictionary<string, FP> CostAttributes { get; } =
            new Dictionary<string, FP>();

        /// <summary>初始化技能基础属性。</summary>
        /// <param name="abilityId">技能唯一 ID；不可为 null 或空。</param>
        /// <param name="cooldown">冷却时间（秒）；0 表示无冷却。</param>
        /// <param name="requiredTags">激活前提标签列表；为 null 时视为空列表。</param>
        /// <param name="blockedTags">阻止激活的标签列表；为 null 时视为空列表。</param>
        protected GameplayAbility(
            string abilityId,
            FP cooldown,
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
        /// <param name="spec">授予 Spec；可为 null（兼容旧 API）。</param>
        /// <returns>检查结果；成功则可进入 Commit。</returns>
        public virtual AbilityActivationResult CanActivate(
            AbilitySystemComponent owner,
            in AbilityActivationContext context,
            GameplayAbilitySpec spec = null)
        {
            if (owner.IsDead)
            {
                return AbilityActivationResult.Failed(AbilityActivationFailureReason.Dead);
            }

            if (owner.Tags.HasTag(new GameplayTag(BattleConstants.TagStunned)) ||
                owner.Tags.HasTag(new GameplayTag(BattleConstants.TagSilenced)) ||
                owner.Tags.HasTag(new GameplayTag(BattleConstants.TagKnockedDown)))
            {
                return AbilityActivationResult.Failed(AbilityActivationFailureReason.CrowdControlled);
            }

            if (owner.CooldownRemaining(spec != null ? spec.Def.GetCooldownId() : GetCooldownId()) > FP.Zero)
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

            if (CostAttributes != null && CostAttributes.Count > 0 && !owner.CanAffordCost(CostAttributes))
            {
                return AbilityActivationResult.Failed(AbilityActivationFailureReason.InsufficientResource);
            }

            return AbilityActivationResult.Succeeded();
        }

        /// <summary>
        /// Commit 阶段：默认启动冷却。返回 false 时激活流程中止（如资源不足，阶段 2 Cost GE 使用）。
        /// </summary>
        /// <param name="owner">持有该技能的 ASC。</param>
        /// <param name="instance">激活实例。</param>
        /// <param name="battle">战斗上下文。</param>
        /// <returns>Commit 成功返回 true。</returns>
        public virtual bool Commit(
            AbilitySystemComponent owner,
            ActiveAbilityInstance instance,
            BattleContext battle)
        {
            owner.StartCooldown(instance.Spec.Def.GetCooldownId(), Cooldown, battle.Presentation);
            return true;
        }

        /// <summary>实际冷却键。</summary>
        /// <returns>共享组名或技能 ID。</returns>
        public string GetCooldownId() => string.IsNullOrEmpty(CooldownId) ? AbilityId : CooldownId;

        /// <summary>执行技能激活逻辑（写入命令缓冲 / 启动 AbilityTask）。</summary>
        /// <param name="owner">持有该技能的 ASC。</param>
        /// <param name="instance">激活实例（含 Context 与 ActivationInfo）。</param>
        /// <param name="battle">当前帧战斗上下文。</param>
        public abstract void Activate(
            AbilitySystemComponent owner,
            ActiveAbilityInstance instance,
            BattleContext battle);

        /// <summary>技能结束时的清理回调；ASC 在 End/Cancel 时 garant 调用。</summary>
        /// <param name="owner">持有该技能的 ASC。</param>
        /// <param name="instance">激活实例；Cancel 时 State 为 Cancelled。</param>
        public virtual void End(AbilitySystemComponent owner, ActiveAbilityInstance instance) { }
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
        public FP Range { get; }

        /// <summary>构造技能激活上下文。</summary>
        /// <param name="origin">施放起点（世界坐标）。</param>
        /// <param name="direction">施放方向；零向量时自动设为 <see cref="Vector3.forward"/>。</param>
        /// <param name="primaryTarget">主目标 ID；无目标传 default。</param>
        /// <param name="range">有效范围（米）；0 表示由技能自定义。</param>
        public AbilityActivationContext(
            Vector3 origin,
            Vector3 direction,
            ActorId primaryTarget = default,
            FP range = default)
        {
            Origin = origin;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
            PrimaryTarget = primaryTarget;
            Range = range;
        }
    }
}
