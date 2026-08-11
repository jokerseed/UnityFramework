using Framework.GAS;

namespace Framework.GAS.Combat
{
    public interface IDamageProcessor
    {
        DamageContext Process(DamageContext context, AbilitySystemComponent source, AbilitySystemComponent target);
    }

    /// <summary>默认伤害管线：免疫 → 护甲减免 → 下限 0。</summary>
    public sealed class DefaultDamageProcessor : IDamageProcessor
    {
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
