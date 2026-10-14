using System;
using System.Collections.Generic;
using cfg;
using Framework.Battle;
using Framework.GAS;

namespace Framework.GamePlay.Data
{
    /// <summary>把效果目录应用到 GAS <see cref="AbilitySystemComponent"/>。Luban 表是默认后端。</summary>
    public static class BattleConfigApplier
    {
        /// <summary>对 ASC 应用 Luban 配置的效果。</summary>
        /// <param name="asc">目标 ASC。</param>
        /// <param name="effectId">效果 ID。</param>
        /// <param name="source">来源 Actor。</param>
        /// <param name="tables">Luban 表。</param>
        /// <param name="battle">战斗上下文。</param>
        /// <exception cref="KeyNotFoundException">效果 ID 不在表中。</exception>
        public static void ApplyEffect(
            AbilitySystemComponent asc,
            string effectId,
            ActorId source,
            CfgTables tables,
            BattleContext battle)
        {
            ApplyEffect(asc, effectId, source, new LubanEffectCatalog(tables), battle);
        }

        /// <summary>对 ASC 应用目录中的效果。</summary>
        /// <param name="asc">目标 ASC。</param>
        /// <param name="effectId">效果 ID。</param>
        /// <param name="source">来源 Actor。</param>
        /// <param name="catalog">效果目录；不可为 null。</param>
        /// <param name="battle">战斗上下文。</param>
        /// <exception cref="ArgumentNullException"><paramref name="catalog"/> 为 null。</exception>
        /// <exception cref="KeyNotFoundException">效果 ID 不在目录中。</exception>
        public static void ApplyEffect(
            AbilitySystemComponent asc,
            string effectId,
            ActorId source,
            IBattleEffectCatalog catalog,
            BattleContext battle)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            if (!catalog.TryCreateEffect(effectId, out var def))
            {
                throw new KeyNotFoundException($"Effect config not found: {effectId}");
            }

            asc.ApplyEffect(def, source, battle.Presentation);
        }
    }
}
