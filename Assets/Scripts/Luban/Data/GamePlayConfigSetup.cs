using System;
using System.Collections.Generic;
using cfg;
using Framework.Config;
using Framework.Battle;
using Framework.GAS.Abilities;

namespace Framework.GamePlay.Data
{
    /// <summary>将配置目录装配到 <see cref="GamePlayFramework"/> 的扩展方法。Luban 表是默认后端。</summary>
    public static class GamePlayConfigSetup
    {
        /// <summary>为 Actor 注册 Luban 配置的技能。</summary>
        /// <param name="framework">玩法框架。</param>
        /// <param name="actorId">Actor ID。</param>
        /// <param name="teamId">队伍 ID。</param>
        /// <param name="abilityIds">技能 ID 列表。</param>
        /// <param name="tables">Luban 表；为 null 时通过 <see cref="ConfigManager.LoadTables{TTables}"/> 按需加载。</param>
        /// <exception cref="InvalidOperationException">表未能加载，或技能/效果引用了不存在的效果。</exception>
        /// <exception cref="KeyNotFoundException">某个技能 ID 不在表中。</exception>
        public static void RegisterActorAbilities(
            this GamePlayFramework framework,
            ActorId actorId,
            int teamId,
            IReadOnlyList<string> abilityIds,
            CfgTables tables = null)
        {
            tables ??= ConfigManager.Instance.LoadTables<CfgTables>();
            if (tables == null)
            {
                throw new InvalidOperationException("Config tables are not loaded. Call ConfigManager.LoadTables<CfgTables>() first.");
            }

            if (framework.EffectCatalog == null)
            {
                // 第一次装配时校验配表引用：缺失的效果在这里报错，而不是战斗中命中时才发现。
                BattleConfigValidator.Validate(tables);
                framework.EffectCatalog = new LubanEffectCatalog(tables);
            }
            var catalog = new LubanAbilityCatalog(
                tables,
                framework.QueryNearestEnemy,
                framework.QueryEnemiesInCone);
            RegisterActorAbilities(framework, actorId, teamId, abilityIds, catalog);
        }

        /// <summary>为 Actor 注册目录中的技能。调用方提供任意 <see cref="IBattleAbilityCatalog"/>。</summary>
        /// <param name="framework">玩法框架。</param>
        /// <param name="actorId">Actor ID。</param>
        /// <param name="teamId">队伍 ID，传给目录。</param>
        /// <param name="abilityIds">技能 ID 列表。</param>
        /// <param name="catalog">技能目录；不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="catalog"/> 为 null。</exception>
        /// <exception cref="KeyNotFoundException">某个技能 ID 不在目录中。</exception>
        public static void RegisterActorAbilities(
            this GamePlayFramework framework,
            ActorId actorId,
            int teamId,
            IReadOnlyList<string> abilityIds,
            IBattleAbilityCatalog catalog)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            for (var i = 0; i < abilityIds.Count; i++)
            {
                var abilityId = abilityIds[i];
                if (!catalog.TryCreateAbility(abilityId, teamId, out var def))
                {
                    throw new KeyNotFoundException($"Ability config not found: {abilityId}");
                }

                framework.GiveAbility(actorId, def);
            }
        }
    }
}
