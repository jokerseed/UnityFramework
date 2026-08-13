using System.Collections.Generic;
using cfg;
using Framework.Core;
using Framework.GAS;

namespace Framework.GamePlay.Data
{
    /// <summary>将 Luban 效果配置应用到 GAS <see cref="AbilitySystemComponent"/>。</summary>
    public static class BattleConfigApplier
    {
        /// <summary>对 ASC 应用 Luban 配置的效果。</summary>
        /// <param name="asc">目标 ASC。</param>
        /// <param name="effectId">效果 ID。</param>
        /// <param name="source">来源 Actor。</param>
        /// <param name="tables">Luban 表。</param>
        /// <param name="battle">战斗上下文。</param>
        public static void ApplyEffect(
            AbilitySystemComponent asc,
            string effectId,
            ActorId source,
            CfgTables tables,
            BattleContext battle)
        {
            if (!tables.CfgTbEffect.DataMap.TryGetValue(effectId, out var def))
            {
                throw new KeyNotFoundException($"Effect config not found: {effectId}");
            }

            asc.ApplyEffect(EffectConfigFactory.CreateDef(def), source, battle.Presentation);
        }
    }
}
