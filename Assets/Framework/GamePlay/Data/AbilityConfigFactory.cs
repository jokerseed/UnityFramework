using cfg;
using Framework.Core;
using Framework.GAS.Abilities;
using Framework.GAS.Abilities.Builtin;
using UnityEngine;

namespace Framework.GamePlay.Data
{
    /// <summary>从 Luban 技能表创建 <see cref="GameplayAbility"/> / <see cref="GameplayAbilityDef"/>。</summary>
    public sealed class AbilityConfigFactory
    {
        readonly System.Func<ActorId, Vector3, float, ActorId> _queryNearestEnemy;

        /// <summary>构造技能配置工厂。</summary>
        /// <param name="queryNearestEnemy">最近敌人查询委托。</param>
        public AbilityConfigFactory(System.Func<ActorId, Vector3, float, ActorId> queryNearestEnemy)
        {
            _queryNearestEnemy = queryNearestEnemy;
        }

        /// <summary>创建技能定义。</summary>
        /// <param name="def">Luban 技能行。</param>
        /// <param name="teamId">队伍 ID。</param>
        /// <returns>技能定义。</returns>
        public GameplayAbilityDef CreateDef(CfgAbilityDef def, int teamId)
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
        public GameplayAbility Create(CfgAbilityDef def, int teamId)
        {
            switch (def.Type)
            {
                case CfgAbilityType.Projectile:
                    return new ProjectileAbility(
                        def.Id,
                        def.Cooldown,
                        def.Speed,
                        def.Radius,
                        def.Lifetime,
                        def.Damage,
                        teamId);
                case CfgAbilityType.Melee:
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
}
