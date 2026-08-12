using System.Collections.Generic;
using cfg;
using Framework.Core;
using Framework.GAS;
using Framework.GAS.Effects;
using Framework.Res;

namespace Framework.Config
{
    /// <summary>战斗配置表加载与效果应用辅助。</summary>
    public static class BattleConfigBootstrap
    {
        /// <summary>Editor 直读配置表。</summary>
        /// <returns>Luban Tables。</returns>
        public static Tables LoadTables() => BattleConfigLoader.LoadDefault();

        /// <summary>从 YooAsset 加载配置表。</summary>
        /// <param name="resourceManager">资源管理器。</param>
        /// <param name="cacheAssets">是否缓存 bytes。</param>
        /// <returns>Luban Tables。</returns>
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

        /// <summary>运行时缓存的 Tables；由 <see cref="LoadRuntimeTables"/> 赋值。</summary>
        public static Tables Tables { get; private set; }

        /// <summary>加载并缓存运行时 Tables。</summary>
        /// <param name="resourceManager">资源管理器。</param>
        public static void LoadRuntimeTables(ResourceManager resourceManager)
        {
            Tables = LoadTables(resourceManager);
        }

        /// <summary>释放运行时 Tables 缓存。</summary>
        public static void UnloadRuntimeTables()
        {
            Tables = null;
        }

        /// <summary>对 ASC 应用 Luban 效果。</summary>
        /// <param name="asc">目标 ASC。</param>
        /// <param name="effectId">效果 ID。</param>
        /// <param name="source">来源 Actor。</param>
        /// <param name="tables">Luban 表。</param>
        /// <param name="battle">战斗上下文。</param>
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

            asc.ApplyEffect(EffectFactory.CreateDef(def), source, battle.Presentation);
        }
    }
}
