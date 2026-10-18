using System;
using System.Collections.Generic;
using cfg;

namespace Framework.GamePlay.Data
{
    /// <summary>
    /// 战斗配置的引用校验：技能和效果里引用的效果 ID 都必须存在于效果表。
    /// 在装配时调用，让配表错误在加载阶段暴露，而不是在战斗进行中才找不到效果。
    /// </summary>
    public static class BattleConfigValidator
    {
        /// <summary>找出全部缺失的效果引用。</summary>
        /// <param name="tables">Luban 表；不可为 null。</param>
        /// <returns>缺失引用的描述；为空表示通过。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tables"/> 为 null。</exception>
        public static List<string> FindMissingEffectReferences(CfgTables tables)
        {
            if (tables == null)
            {
                throw new ArgumentNullException(nameof(tables));
            }

            var effects = tables.CfgTbEffect.DataMap;
            var missing = new List<string>();

            void Check(string effectId, string owner)
            {
                if (!string.IsNullOrEmpty(effectId) && !effects.ContainsKey(effectId))
                {
                    missing.Add($"{owner} -> '{effectId}'");
                }
            }

            foreach (var ability in tables.CfgTbAbility.DataList)
            {
                Check(ability.HitEffectId, $"ability '{ability.Id}'.HitEffectId");
                Check(ability.ComboEffectId, $"ability '{ability.Id}'.ComboEffectId");
            }

            foreach (var effect in tables.CfgTbEffect.DataList)
            {
                if (effect.ExecutionType == CfgEffectExecutionType.ApplyEffect)
                {
                    Check(effect.ExecutionEffectId, $"effect '{effect.Id}'.ExecutionEffectId");
                }
            }

            return missing;
        }

        /// <summary>校验效果引用，有缺失就抛出并列出全部缺失项。</summary>
        /// <param name="tables">Luban 表；不可为 null。</param>
        /// <exception cref="InvalidOperationException">存在缺失的效果引用。</exception>
        public static void Validate(CfgTables tables)
        {
            var missing = FindMissingEffectReferences(tables);
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "Battle config references missing effects:\n  " + string.Join("\n  ", missing));
            }
        }
    }
}
