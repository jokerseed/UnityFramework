using System;
using System.Collections.Generic;
using cfg;
using Framework.Battle;
using Framework.FixedMath;
using Framework.GAS.Abilities;
using Framework.GAS.Abilities.Builtin;
using Framework.GAS.Tags;
using Framework.GAS.Targeting;
using UnityEngine;

namespace Framework.GamePlay.Data
{
    /// <summary>
    /// 从 Luban 技能表创建 <see cref="GameplayAbility"/> / <see cref="GameplayAbilityDef"/>。
    /// 表里的 float 一律经 <see cref="FixedConfig.FromConfig(float)"/> 量化后再交给技能，服务端用 double 读同一张表时结果一致。
    /// </summary>
    public sealed class AbilityConfigFactory
    {
        readonly System.Func<ActorId, TSVector, FP, ActorId> _queryNearestEnemy;
        readonly ConeEnemyQuery _queryCone;

        /// <summary>构造技能配置工厂。</summary>
        /// <param name="queryNearestEnemy">最近敌人查询委托。</param>
        /// <param name="queryCone">扇形敌对查询委托；近战扇形技能需要。</param>
        public AbilityConfigFactory(
            System.Func<ActorId, TSVector, FP, ActorId> queryNearestEnemy,
            ConeEnemyQuery queryCone = null)
        {
            _queryNearestEnemy = queryNearestEnemy;
            _queryCone = queryCone;
        }

        /// <summary>创建技能定义。</summary>
        /// <param name="def">Luban 技能行。</param>
        /// <param name="teamId">队伍 ID。</param>
        /// <returns>技能定义。</returns>
        public GameplayAbilityDef CreateDef(CfgAbilityDef def, int teamId)
        {
            var cost = ParseCost(def.CostAttribute, def.CostAmount);
            return new GameplayAbilityDef(
                def.Id,
                Fx(def.Cooldown),
                () => Create(def, teamId),
                ParseTags(def.RequiredTags),
                ParseTags(def.BlockedTags),
                costAttributes: cost,
                cooldownId: string.IsNullOrEmpty(def.CooldownGroup) ? null : def.CooldownGroup,
                assetTags: ParseTags(def.AssetTags),
                activationOwnedTags: ParseTags(def.OwnedTags),
                cancelAbilitiesWithTags: ParseTags(def.CancelTags));
        }

        /// <summary>创建技能实例。</summary>
        /// <param name="def">Luban 技能行。</param>
        /// <param name="teamId">队伍 ID。</param>
        /// <returns>技能实例。</returns>
        public GameplayAbility Create(CfgAbilityDef def, int teamId)
        {
            var damageType = (BattleDamageType)(int)def.DamageType;
            var cost = ParseCost(def.CostAttribute, def.CostAmount);
            var required = ParseTags(def.RequiredTags);
            var blocked = ParseTags(def.BlockedTags);
            var cooldown = Fx(def.Cooldown);
            var damage = Fx(def.Damage);
            var projectile = new ProjectileAbility(
                def.Id,
                cooldown,
                Fx(def.Speed),
                Fx(def.Radius),
                Fx(def.Lifetime),
                damage,
                teamId,
                def.PierceCount,
                Fx(def.ExplodeRadius),
                def.HitEffectId,
                damageType,
                required,
                blocked,
                cost);

            switch (def.Type)
            {
                case CfgAbilityType.Projectile:
                    return WrapChannel(def, projectile);
                case CfgAbilityType.PierceProjectile:
                    return WrapChannel(def, new ProjectileAbility(
                        def.Id, cooldown, Fx(def.Speed), Fx(def.Radius), Fx(def.Lifetime), damage, teamId,
                        Math.Max(1, def.PierceCount), Fx(def.ExplodeRadius), def.HitEffectId, damageType, required, blocked, cost));
                case CfgAbilityType.ExplodeProjectile:
                    return WrapChannel(def, new ProjectileAbility(
                        def.Id, cooldown, Fx(def.Speed), Fx(def.Radius), Fx(def.Lifetime), damage, teamId,
                        def.PierceCount, Fx(def.ExplodeRadius > 0f ? def.ExplodeRadius : 1.5f), def.HitEffectId, damageType, required, blocked, cost));
                case CfgAbilityType.Melee:
                    if (def.HalfAngle > 0f)
                    {
                        return new MeleeSweepAbility(
                            def.Id,
                            cooldown,
                            damage,
                            Fx(def.Range),
                            Fx(def.HalfAngle),
                            _queryCone,
                            Fx(def.ChannelTime),
                            def.Lifetime > 0f ? Fx(def.Lifetime) : MeleeSweepAbility.DefaultHitDuration,
                            Fx(def.RecoveryTime),
                            Fx(def.Knockback),
                            def.HitEffectId,
                            string.IsNullOrEmpty(def.ComboEffectId) ? null : def.ComboEffectId,
                            damageType,
                            required,
                            blocked,
                            cost);
                    }

                    return new MeleeStrikeAbility(
                        def.Id, cooldown, damage, Fx(def.Range),
                        new MeleeTargetSelector(_queryNearestEnemy),
                        damageType, required, blocked, cost);
                case CfgAbilityType.Dash:
                    return new DashAbility(
                        def.Id,
                        cooldown,
                        Fx(def.Knockback > 0f ? def.Knockback : 3f),
                        Fx(def.RecoveryTime > 0f ? def.RecoveryTime : 0.28f),
                        def.HitEffectId,
                        required,
                        blocked);
                case CfgAbilityType.AoeCircle:
                    return new CircleAoeAbility(
                        def.Id, cooldown, damage, Fx(def.Radius), teamId, def.HitEffectId, damageType, required, blocked, cost);
                case CfgAbilityType.AoeCone:
                    return new ConeAoeAbility(
                        def.Id, cooldown, damage, Fx(def.Range), Fx(def.HalfAngle), teamId, def.HitEffectId, damageType, required, blocked, cost);
                default:
                    throw new NotSupportedException($"Unsupported ability type: {def.Type} ({def.Id})");
            }
        }

        static GameplayAbility WrapChannel(CfgAbilityDef def, ProjectileAbility projectile)
        {
            if (def.ChannelTime <= 0f)
            {
                return projectile;
            }

            return new ChanneledProjectileAbility(def.Id, Fx(def.Cooldown), Fx(def.ChannelTime), projectile);
        }

        static FP Fx(float value) => FixedConfig.FromConfig(value);

        static IReadOnlyList<GameplayTag> ParseTags(string csv)
        {
            if (string.IsNullOrEmpty(csv))
            {
                return Array.Empty<GameplayTag>();
            }

            var parts = csv.Split(',');
            var list = new List<GameplayTag>(parts.Length);
            for (var i = 0; i < parts.Length; i++)
            {
                var name = parts[i].Trim();
                if (name.Length > 0)
                {
                    list.Add(new GameplayTag(name));
                }
            }

            return list;
        }

        static IReadOnlyDictionary<string, FP> ParseCost(string attribute, float amount)
        {
            if (string.IsNullOrEmpty(attribute) || amount <= 0f)
            {
                return new Dictionary<string, FP>();
            }

            return new Dictionary<string, FP> { [attribute] = Fx(amount) };
        }
    }
}
