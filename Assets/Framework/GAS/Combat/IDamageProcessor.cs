using Framework.GAS;

namespace Framework.GAS.Combat
{
    /// <summary>伤害处理管线接口，将原始伤害上下文转换为最终结算上下文（免疫检测、护甲减免等）。</summary>
    public interface IDamageProcessor
    {
        /// <summary>处理伤害并返回包含最终伤害量的新上下文。</summary>
        /// <param name="context">原始伤害上下文。</param>
        /// <param name="source">来源单位的 ASC；可为 null（如环境伤害）。</param>
        /// <param name="target">目标单位的 ASC；不可为 null。</param>
        /// <returns>经处理后的伤害上下文，最终伤害量已更新。</returns>
        DamageContext Process(DamageContext context, AbilitySystemComponent source, AbilitySystemComponent target);
    }

    /// <summary>默认伤害管线：免疫 → 护甲减免 → 下限 0。</summary>
    public sealed class DefaultDamageProcessor : IDamageProcessor
    {
        /// <summary>按顺序检查免疫标签、减去护甲值并钳制到 0，返回最终伤害上下文。</summary>
        /// <param name="context">原始伤害上下文。</param>
        /// <param name="source">来源单位的 ASC；可为 null。</param>
        /// <param name="target">目标单位的 ASC；不可为 null。</param>
        /// <returns>包含最终伤害量的新上下文；免疫时最终伤害为 0。</returns>
        public DamageContext Process(DamageContext context, AbilitySystemComponent source, AbilitySystemComponent target)
        {
            if (target.Tags.HasTag(new Tags.GameplayTag(Core.BattleConstants.TagImmuneDamage)) ||
                target.Tags.HasTag(new Tags.GameplayTag(Core.BattleConstants.TagDead)))
            {
                return context.WithFinalDamage(0f);
            }

            var defense = target.Attributes.GetCurrentValue(Core.BattleConstants.Defense);
            var final = context.RawDamage - defense;
            if (final < 0f)
            {
                final = 0f;
            }

            return context.WithFinalDamage(final);
        }
    }
}
