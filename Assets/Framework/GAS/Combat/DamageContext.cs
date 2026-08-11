using Framework.Core;

namespace Framework.GAS.Combat
{
    public readonly struct DamageContext
    {
        public ActorId Source { get; }
        public ActorId Target { get; }
        public float RawDamage { get; }
        public string AbilityId { get; }
        public float FinalDamage { get; }

        public DamageContext(ActorId source, ActorId target, float rawDamage, string abilityId, float finalDamage = 0f)
        {
            Source = source;
            Target = target;
            RawDamage = rawDamage;
            AbilityId = abilityId;
            FinalDamage = finalDamage > 0f ? finalDamage : rawDamage;
        }

        public DamageContext WithFinalDamage(float finalDamage) =>
            new DamageContext(Source, Target, RawDamage, AbilityId, finalDamage);
    }
}
