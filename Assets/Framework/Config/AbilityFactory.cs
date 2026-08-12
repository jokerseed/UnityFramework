using System.Collections.Generic;
using cfg;
using Framework.Core;
using Framework.GAS.Abilities;
using Framework.GAS.Abilities.Builtin;
using Framework.GAS.Effects;
using Framework.GAS.Tags;

namespace Framework.Config
{
    /// <summary>从 Luban 表创建 <see cref="GameplayAbility"/> / <see cref="GameplayAbilityDef"/>。</summary>
    public sealed class AbilityFactory
    {
        readonly System.Func<ActorId, UnityEngine.Vector3, float, ActorId> _queryNearestEnemy;

        /// <summary>构造技能工厂。</summary>
        /// <param name="queryNearestEnemy">最近敌人查询委托。</param>
        public AbilityFactory(System.Func<ActorId, UnityEngine.Vector3, float, ActorId> queryNearestEnemy)
        {
            _queryNearestEnemy = queryNearestEnemy;
        }

        /// <summary>创建技能定义。</summary>
        /// <param name="def">Luban 技能行。</param>
        /// <param name="teamId">队伍 ID。</param>
        /// <returns>技能定义。</returns>
        public GameplayAbilityDef CreateDef(AbilityDef def, int teamId)
        {
            return new GameplayAbilityDef(
                def.Id,
                def.Cooldown,
                () => Create(def, teamId));
        }

        /// <summary>创建技能实例。</summary>
        /// <param name="def">Luban 技能行。</param>
        /// <param name="teamId">队伍 ID。</param>
        /// <returns>技能实例。</returns>
        public GameplayAbility Create(AbilityDef def, int teamId)
        {
            switch (def.Type)
            {
                case AbilityType.Projectile:
                    return new ProjectileAbility(
                        def.Id,
                        def.Cooldown,
                        def.Speed,
                        def.Radius,
                        def.Lifetime,
                        def.Damage,
                        teamId);
                case AbilityType.Melee:
                    return new MeleeStrikeAbility(
                        def.Id,
                        def.Cooldown,
                        def.Damage,
                        def.Range,
                        new GAS.Targeting.MeleeTargetSelector(_queryNearestEnemy));
                default:
                    throw new System.NotSupportedException($"Unsupported ability type: {def.Type} ({def.Id})");
            }
        }
    }

    /// <summary>从 Luban 表创建 <see cref="GameplayEffectDef"/>。</summary>
    public static class EffectFactory
    {
        /// <summary>创建效果定义。</summary>
        /// <param name="def">Luban 效果行。</param>
        /// <returns>效果定义。</returns>
        public static GameplayEffectDef CreateDef(EffectDef def)
        {
            var modifiers = new List<EffectModifier>();
            if (!string.IsNullOrEmpty(def.ModAttribute))
            {
                modifiers.Add(new EffectModifier(
                    def.ModAttribute,
                    (EffectModifierOperation)def.ModOperation,
                    def.ModMagnitude));
            }

            return new GameplayEffectDef(
                def.Id,
                MapDuration(def.DurationType),
                def.Duration,
                modifiers,
                stackingPolicy: MapStacking(def.Stacking));
        }

        /// <summary>创建运行时 Spec（兼容旧 API）。</summary>
        /// <param name="def">Luban 效果行。</param>
        /// <returns>运行时 Spec。</returns>
        public static GameplayEffectSpec Create(EffectDef def) => CreateDef(def).ToRuntimeSpec();

        static EffectDurationPolicy MapDuration(EffectDurationType type)
        {
            switch (type)
            {
                case EffectDurationType.Instant: return EffectDurationPolicy.Instant;
                case EffectDurationType.Duration: return EffectDurationPolicy.Duration;
                case EffectDurationType.Infinite: return EffectDurationPolicy.Infinite;
                default: throw new System.NotSupportedException($"Unknown duration type: {type}");
            }
        }

        static EffectStackingPolicy MapStacking(EffectStackingType type)
        {
            switch (type)
            {
                case EffectStackingType.None: return EffectStackingPolicy.None;
                case EffectStackingType.RefreshDuration: return EffectStackingPolicy.RefreshDuration;
                case EffectStackingType.StackCount: return EffectStackingPolicy.StackCount;
                case EffectStackingType.AggregateBySource: return EffectStackingPolicy.AggregateBySource;
                default: throw new System.NotSupportedException($"Unknown stacking type: {type}");
            }
        }
    }
}
