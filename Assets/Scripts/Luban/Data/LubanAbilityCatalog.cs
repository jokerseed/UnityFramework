using System;
using cfg;
using Framework.Battle;
using Framework.FixedMath;
using Framework.GAS.Abilities;
using Framework.GAS.Targeting;

namespace Framework.GamePlay.Data
{
    /// <summary>默认技能目录：Luban <see cref="CfgTables"/> 经 <see cref="AbilityConfigFactory"/> 映射。</summary>
    public sealed class LubanAbilityCatalog : IBattleAbilityCatalog
    {
        readonly CfgTables _tables;
        readonly AbilityConfigFactory _factory;

        /// <summary>用已加载的技能表与玩法查询委托构造目录。</summary>
        /// <param name="tables">Luban 表；不可为 null。</param>
        /// <param name="queryNearestEnemy">最近敌人查询；近战点选技能需要。</param>
        /// <param name="queryCone">扇形敌对查询；近战扇形技能需要，可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="tables"/> 为 null。</exception>
        public LubanAbilityCatalog(
            CfgTables tables,
            Func<ActorId, TSVector, FP, ActorId> queryNearestEnemy,
            ConeEnemyQuery queryCone = null)
        {
            if (tables == null)
            {
                throw new ArgumentNullException(nameof(tables));
            }

            _tables = tables;
            _factory = new AbilityConfigFactory(queryNearestEnemy, queryCone);
        }

        /// <inheritdoc />
        public bool TryCreateAbility(string abilityId, int teamId, out GameplayAbilityDef ability)
        {
            if (_tables.CfgTbAbility.DataMap.TryGetValue(abilityId, out var def))
            {
                ability = _factory.CreateDef(def, teamId);
                return true;
            }

            ability = null;
            return false;
        }
    }
}
