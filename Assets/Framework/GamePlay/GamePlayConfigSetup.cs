using System.Collections.Generic;
using cfg;
using Framework.Config;
using Framework.Core;
using Framework.GAS.Abilities;

namespace Framework.GamePlay
{
    /// <summary>将 Luban 配置装配到 <see cref="GamePlayFramework"/> 的扩展方法。</summary>
    public static class GamePlayConfigSetup
    {
        /// <summary>为 Actor 注册 Luban 配置的技能。</summary>
        /// <param name="framework">玩法框架。</param>
        /// <param name="actorId">Actor ID。</param>
        /// <param name="teamId">队伍 ID。</param>
        /// <param name="abilityIds">技能 ID 列表。</param>
        /// <param name="tables">Luban 表；为 null 时使用 <see cref="BattleConfigBootstrap.Tables"/>。</param>
        public static void RegisterActorAbilities(
            this GamePlayFramework framework,
            ActorId actorId,
            int teamId,
            IReadOnlyList<string> abilityIds,
            Tables tables = null)
        {
            tables ??= BattleConfigBootstrap.Tables;
            if (tables == null)
            {
                throw new System.InvalidOperationException("Config tables are not loaded. Initialize ConfigModule first.");
            }

            var factory = new AbilityFactory(framework.QueryNearestEnemy);
            for (var i = 0; i < abilityIds.Count; i++)
            {
                var abilityId = abilityIds[i];
                if (!tables.TbAbility.DataMap.TryGetValue(abilityId, out var def))
                {
                    throw new KeyNotFoundException($"Ability config not found: {abilityId}");
                }

                framework.GiveAbility(actorId, factory.CreateDef(def, teamId));
            }
        }
    }
}
