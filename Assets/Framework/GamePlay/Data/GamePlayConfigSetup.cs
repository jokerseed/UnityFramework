using System.Collections.Generic;
using cfg;
using Framework.Config;
using Framework.Core;
using Framework.GAS.Abilities;

namespace Framework.GamePlay.Data
{
    /// <summary>将 Luban 配置装配到 <see cref="GamePlayFramework"/> 的扩展方法。</summary>
    public static class GamePlayConfigSetup
    {
        /// <summary>为 Actor 注册 Luban 配置的技能。</summary>
        /// <param name="framework">玩法框架。</param>
        /// <param name="actorId">Actor ID。</param>
        /// <param name="teamId">队伍 ID。</param>
        /// <param name="abilityIds">技能 ID 列表。</param>
        /// <param name="tables">Luban 表；为 null 时通过 <see cref="ConfigManager.LoadTables"/> 按需加载。</param>
        public static void RegisterActorAbilities(
            this GamePlayFramework framework,
            ActorId actorId,
            int teamId,
            IReadOnlyList<string> abilityIds,
            CfgTables tables = null)
        {
            tables ??= ConfigManager.Instance.LoadTables();
            if (tables == null)
            {
                throw new System.InvalidOperationException("Config tables are not loaded. Call ConfigManager.LoadTables() first.");
            }

            var factory = new AbilityConfigFactory(framework.QueryNearestEnemy);
            for (var i = 0; i < abilityIds.Count; i++)
            {
                var abilityId = abilityIds[i];
                if (!tables.CfgTbAbility.DataMap.TryGetValue(abilityId, out var def))
                {
                    throw new KeyNotFoundException($"Ability config not found: {abilityId}");
                }

                framework.GiveAbility(actorId, factory.CreateDef(def, teamId));
            }
        }
    }
}
