using System;
using cfg;
using Framework.GAS.Effects;

namespace Framework.GamePlay.Data
{
    /// <summary>默认效果目录：Luban <see cref="CfgTables"/> 经 <see cref="EffectConfigFactory"/> 映射。</summary>
    public sealed class LubanEffectCatalog : IBattleEffectCatalog
    {
        readonly CfgTables _tables;

        /// <summary>用已加载的效果表构造目录。</summary>
        /// <param name="tables">Luban 表；不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="tables"/> 为 null。</exception>
        public LubanEffectCatalog(CfgTables tables)
        {
            if (tables == null)
            {
                throw new ArgumentNullException(nameof(tables));
            }

            _tables = tables;
        }

        /// <inheritdoc />
        public bool TryCreateEffect(string effectId, out GameplayEffectDef effect)
        {
            if (_tables.CfgTbEffect.DataMap.TryGetValue(effectId, out var def))
            {
                effect = EffectConfigFactory.CreateDef(def, _tables);
                return true;
            }

            effect = null;
            return false;
        }
    }
}
