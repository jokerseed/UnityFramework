using System;
using System.Collections.Generic;
using Framework.GAS.Tags;

namespace Framework.GAS.Abilities
{
    /// <summary>技能不可变定义，描述 ID、冷却、标签前提及技能实例工厂。</summary>
    public sealed class GameplayAbilityDef
    {
        /// <summary>技能唯一标识符。</summary>
        public string AbilityId { get; }

        /// <summary>技能冷却时间（秒）。</summary>
        public float Cooldown { get; }

        /// <summary>冷却键；空则使用 <see cref="AbilityId"/>。相同键的技能共享冷却 GE。</summary>
        public string CooldownId { get; }

        /// <summary>激活所需的 GameplayTag 列表。</summary>
        public IReadOnlyList<GameplayTag> RequiredTags { get; }

        /// <summary>阻止激活的 GameplayTag 列表。</summary>
        public IReadOnlyList<GameplayTag> BlockedTags { get; }

        /// <summary>技能身份 Tag，供按 Tag 取消/查询。</summary>
        public IReadOnlyList<GameplayTag> AssetTags { get; }

        /// <summary>激活期间授予拥有者的 Tag；结束或取消时移除。</summary>
        public IReadOnlyList<GameplayTag> ActivationOwnedTags { get; }

        /// <summary>激活时取消拥有者其它匹配这些 Tag 的技能。</summary>
        public IReadOnlyList<GameplayTag> CancelAbilitiesWithTags { get; }

        /// <summary>被动触发标签；非空时可通过 <see cref="AbilitySystemComponent.HandleGameplayEvent"/> 激活。</summary>
        public GameplayTag TriggerTag { get; }

        /// <summary>激活消耗（属性名 → 量）。</summary>
        public IReadOnlyDictionary<string, float> CostAttributes { get; }

        readonly Func<GameplayAbility> _abilityFactory;

        /// <summary>创建技能运行时实例。</summary>
        /// <returns>新的 <see cref="GameplayAbility"/> 实例。</returns>
        public GameplayAbility CreateAbility() => _abilityFactory();

        /// <summary>实际冷却 GE 键：有 <see cref="CooldownId"/> 用组名，否则用技能 ID。</summary>
        /// <returns>冷却键。</returns>
        public string GetCooldownId() => string.IsNullOrEmpty(CooldownId) ? AbilityId : CooldownId;

        /// <summary>使用已有技能实例包装为定义（共享同一实例模板）。</summary>
        /// <param name="ability">技能实例；不可为 null。</param>
        public GameplayAbilityDef(GameplayAbility ability)
        {
            if (ability == null)
            {
                throw new ArgumentNullException(nameof(ability));
            }

            AbilityId = ability.AbilityId;
            Cooldown = ability.Cooldown;
            CooldownId = ability.CooldownId;
            RequiredTags = ability.RequiredTags;
            BlockedTags = ability.BlockedTags;
            AssetTags = ability.AssetTags;
            ActivationOwnedTags = ability.ActivationOwnedTags;
            CancelAbilitiesWithTags = ability.CancelAbilitiesWithTags;
            TriggerTag = default;
            CostAttributes = ability.CostAttributes ?? new Dictionary<string, float>();
            _abilityFactory = () => ability;
        }

        /// <summary>使用工厂方法创建技能定义。</summary>
        /// <param name="abilityId">技能唯一 ID。</param>
        /// <param name="cooldown">冷却时间（秒）。</param>
        /// <param name="abilityFactory">创建技能实例的工厂；不可为 null。</param>
        /// <param name="requiredTags">激活前提标签；为 null 时视为空。</param>
        /// <param name="blockedTags">阻止激活标签；为 null 时视为空。</param>
        /// <param name="triggerTag">被动触发标签；无效时表示非被动技能。</param>
        /// <param name="costAttributes">激活消耗；为 null 时无消耗。</param>
        /// <param name="cooldownId">共享冷却键；为 null 或空时使用技能 ID。</param>
        /// <param name="assetTags">技能身份 Tag；为 null 时视为空。</param>
        /// <param name="activationOwnedTags">激活期间授予的 Tag；为 null 时视为空。</param>
        /// <param name="cancelAbilitiesWithTags">激活时要取消的其它技能 Tag；为 null 时视为空。</param>
        public GameplayAbilityDef(
            string abilityId,
            float cooldown,
            Func<GameplayAbility> abilityFactory,
            IReadOnlyList<GameplayTag> requiredTags = null,
            IReadOnlyList<GameplayTag> blockedTags = null,
            GameplayTag triggerTag = default,
            IReadOnlyDictionary<string, float> costAttributes = null,
            string cooldownId = null,
            IReadOnlyList<GameplayTag> assetTags = null,
            IReadOnlyList<GameplayTag> activationOwnedTags = null,
            IReadOnlyList<GameplayTag> cancelAbilitiesWithTags = null)
        {
            if (string.IsNullOrEmpty(abilityId))
            {
                throw new ArgumentException("Ability id is required.", nameof(abilityId));
            }

            if (abilityFactory == null)
            {
                throw new ArgumentNullException(nameof(abilityFactory));
            }

            AbilityId = abilityId;
            Cooldown = cooldown;
            CooldownId = cooldownId;
            RequiredTags = requiredTags ?? Array.Empty<GameplayTag>();
            BlockedTags = blockedTags ?? Array.Empty<GameplayTag>();
            AssetTags = assetTags ?? Array.Empty<GameplayTag>();
            ActivationOwnedTags = activationOwnedTags ?? Array.Empty<GameplayTag>();
            CancelAbilitiesWithTags = cancelAbilitiesWithTags ?? Array.Empty<GameplayTag>();
            TriggerTag = triggerTag;
            CostAttributes = costAttributes ?? new Dictionary<string, float>();
            _abilityFactory = abilityFactory;
        }
    }
}
