using System.Collections.Generic;
using cfg;
using Framework.Bridge;
using Framework.Core;
using Framework.GAS;
using Framework.GAS.Abilities;
using Framework.Res;

namespace Framework.Config
{
    public static class BattleConfigBootstrap
    {
        public static Tables LoadTables() => BattleConfigLoader.LoadDefault();

        public static Tables LoadTables(ResourceManager resourceManager, bool cacheAssets = true)
        {
            if (resourceManager == null)
            {
                throw new System.ArgumentNullException(nameof(resourceManager));
            }

            if (!resourceManager.IsInitialized)
            {
                throw new System.InvalidOperationException("ResourceManager is not initialized.");
            }

            return cacheAssets
                ? BattleConfigLoader.LoadFromBytes(resourceManager.LoadConfigBytesCached)
                : BattleConfigLoader.LoadFromBytes(resourceManager.LoadConfigBytes);
        }

        public static Tables Tables { get; private set; }

        public static void LoadRuntimeTables(ResourceManager resourceManager)
        {
            Tables = LoadTables(resourceManager);
        }

        public static void UnloadRuntimeTables()
        {
            Tables = null;
        }

        public static void RegisterActorAbilities(
            BattleFramework framework,
            ActorId actorId,
            int teamId,
            IReadOnlyList<string> abilityIds,
            Tables tables)
        {
            var factory = new AbilityFactory(framework.QueryNearestEnemy);
            for (var i = 0; i < abilityIds.Count; i++)
            {
                var abilityId = abilityIds[i];
                if (!tables.TbAbility.DataMap.TryGetValue(abilityId, out var def))
                {
                    throw new KeyNotFoundException($"Ability config not found: {abilityId}");
                }

                framework.RegisterAbility(actorId, factory.Create(def, teamId));
            }
        }

        public static void ApplyEffect(
            AbilitySystemComponent asc,
            string effectId,
            ActorId source,
            Tables tables,
            BattleContext battle)
        {
            if (!tables.TbEffect.DataMap.TryGetValue(effectId, out var def))
            {
                throw new KeyNotFoundException($"Effect config not found: {effectId}");
            }

            asc.ApplyEffect(EffectFactory.Create(def), source, battle.Presentation);
        }
    }
}
