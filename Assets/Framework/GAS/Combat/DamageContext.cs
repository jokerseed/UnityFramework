using Framework.Core;

namespace Framework.GAS.Combat
{
    /// <summary>伤害上下文，描述单次伤害事件的来源、目标、原始值与最终结算值，贯穿整个伤害管线。</summary>
    public readonly struct DamageContext
    {
        /// <summary>伤害来源单位标识符。</summary>
        public ActorId Source { get; }

        /// <summary>伤害目标单位标识符。</summary>
        public ActorId Target { get; }

        /// <summary>未经减免的原始伤害量。</summary>
        public float RawDamage { get; }

        /// <summary>造成本次伤害的技能 ID。</summary>
        public string AbilityId { get; }

        /// <summary>经伤害管线处理后的最终伤害量；初始等于 <see cref="RawDamage"/>，由处理器修改。</summary>
        public float FinalDamage { get; }

        /// <summary>构造伤害上下文。</summary>
        /// <param name="source">来源单位 ID。</param>
        /// <param name="target">目标单位 ID。</param>
        /// <param name="rawDamage">原始伤害量。</param>
        /// <param name="abilityId">施放技能 ID。</param>
        /// <param name="finalDamage">最终伤害量；≤0 时自动取 <paramref name="rawDamage"/>。</param>
        public DamageContext(ActorId source, ActorId target, float rawDamage, string abilityId, float finalDamage = 0f)
        {
            Source = source;
            Target = target;
            RawDamage = rawDamage;
            AbilityId = abilityId;
            FinalDamage = finalDamage > 0f ? finalDamage : rawDamage;
        }

        /// <summary>返回一个仅替换 <see cref="FinalDamage"/> 的新上下文（不可变模式）。</summary>
        /// <param name="finalDamage">新的最终伤害量。</param>
        /// <returns>拷贝后替换了最终伤害的新上下文。</returns>
        public DamageContext WithFinalDamage(float finalDamage) =>
            new DamageContext(Source, Target, RawDamage, AbilityId, finalDamage);
    }
}
