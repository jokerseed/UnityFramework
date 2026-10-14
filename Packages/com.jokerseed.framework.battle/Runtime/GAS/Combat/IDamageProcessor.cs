using Framework.FixedMath;
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

    /// <summary>默认伤害管线：免疫 → 护盾 → 护甲/魔抗 → 易伤 → 暴击。</summary>
    public sealed class DefaultDamageProcessor : IDamageProcessor
    {
        /// <summary>按顺序检查免疫、护盾、护甲并可选暴击，返回最终伤害上下文。</summary>
        /// <param name="context">原始伤害上下文。</param>
        /// <param name="source">来源单位的 ASC；可为 null。</param>
        /// <param name="target">目标单位的 ASC；不可为 null。</param>
        /// <returns>包含最终伤害量的新上下文；免疫时最终伤害为 0。</returns>
        public DamageContext Process(DamageContext context, AbilitySystemComponent source, AbilitySystemComponent target)
        {
            if (target.Tags.HasTag(new Tags.GameplayTag(Core.BattleConstants.TagImmuneDamage)) ||
                target.Tags.HasTag(new Tags.GameplayTag(Core.BattleConstants.TagDead)))
            {
                return context.WithPipelineResult(FP.Zero, false, FP.Zero);
            }

            var remaining = context.RawDamage > FP.Zero
                ? context.RawDamage
                : source?.Attributes.GetCurrentValue(Core.BattleConstants.Attack) ?? FP.Zero;

            var shield = target.Attributes.GetCurrentValue(Core.BattleConstants.Shield);
            var absorbed = FP.Zero;
            if (shield > FP.Zero && remaining > FP.Zero)
            {
                absorbed = remaining < shield ? remaining : shield;
                remaining -= absorbed;
                var shieldAttr = target.Attributes.GetOrCreate(Core.BattleConstants.Shield);
                shieldAttr.SetCurrentValue(shield - absorbed);
            }

            var resistName = context.DamageType == Core.BattleDamageType.Magical
                ? Core.BattleConstants.MagicDefense
                : Core.BattleConstants.Defense;
            var resist = target.Attributes.GetCurrentValue(resistName);
            remaining -= resist;
            if (remaining < FP.Zero)
            {
                remaining = FP.Zero;
            }

            if (remaining > FP.Zero)
            {
                var takenMul = FP.One;
                if (target.Attributes.TryGet(Core.BattleConstants.IncomingDamageMultiplier, out var takenAttr))
                {
                    takenMul = takenAttr.CurrentValue;
                    if (takenMul < FP.Zero)
                    {
                        takenMul = FP.Zero;
                    }
                }

                remaining *= takenMul;
            }

            var isCrit = false;
            var critChance = source?.Attributes.GetCurrentValue(Core.BattleConstants.CritChance) ?? FP.Zero;
            var random = source?.Random ?? target.Random;
            if (critChance > FP.Zero && remaining > FP.Zero && random != null && random.Next01() < critChance)
            {
                var multiplier = source.Attributes.GetCurrentValue(Core.BattleConstants.CritMultiplier);
                if (multiplier <= FP.Zero)
                {
                    multiplier = Core.BattleConstants.DefaultCritMultiplier;
                }

                remaining *= multiplier;
                isCrit = true;
            }

            return context.WithPipelineResult(remaining, isCrit, absorbed);
        }
    }
}
