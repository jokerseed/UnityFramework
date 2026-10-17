using Framework.Battle;
using Framework.Events;
using Framework.FixedMath;
using Framework.GAS;
using Framework.GAS.Abilities;
using Framework.GAS.Abilities.Builtin;
using Framework.GAS.Effects;
using NUnit.Framework;

namespace Framework.Tests
{
    /// <summary>即时属性、效果句柄和技能实例不要串到别的 ASC。</summary>
    public sealed class GasRuntimeTests
    {
        [Test]
        public void InstantAttackStaysAfterAnotherAttributeRecalculates()
        {
            var asc = new AbilitySystemComponent(new ActorId(1));
            asc.InitializeCombatAttributes((FP)100, (FP)50);
            asc.Attributes.GetOrCreate(BattleConstants.Attack, (FP)10);
            var bus = new ZeroGcEventBus();
            var instant = new GameplayEffectDef(
                "instant-atk",
                EffectDurationPolicy.Instant,
                FP.Zero,
                new[]
                {
                    new EffectModifier(BattleConstants.Attack, EffectModifierOperation.Add, 4f)
                });

            Assert.IsTrue(asc.ApplyEffect(instant, new ActorId(1), bus));
            Assert.AreEqual((FP)14, asc.Attributes.GetBaseValue(BattleConstants.Attack));
            Assert.AreEqual((FP)14, asc.Attributes.GetCurrentValue(BattleConstants.Attack));

            var duration = new GameplayEffectDef(
                "def-buff",
                EffectDurationPolicy.Duration,
                (FP)5,
                new[]
                {
                    new EffectModifier(BattleConstants.Defense, EffectModifierOperation.Add, 3f)
                });
            Assert.IsTrue(asc.ApplyEffect(duration, new ActorId(1), bus));
            Assert.AreEqual((FP)14, asc.Attributes.GetCurrentValue(BattleConstants.Attack));
            Assert.AreEqual((FP)3, asc.Attributes.GetCurrentValue(BattleConstants.Defense));
        }

        [Test]
        public void EffectRuntimeIdIsPerAscAndAbilityInstancesAreNotShared()
        {
            var left = new AbilitySystemComponent(new ActorId(1));
            var right = new AbilitySystemComponent(new ActorId(2));
            var bus = new ZeroGcEventBus();
            var def = new GameplayEffectDef(
                "buff",
                EffectDurationPolicy.Duration,
                (FP)2,
                new[]
                {
                    new EffectModifier(BattleConstants.Defense, EffectModifierOperation.Add, 1f)
                });
            var leftSpec = def.ToRuntimeSpec();
            var rightSpec = def.ToRuntimeSpec();
            Assert.AreEqual(0, leftSpec.RuntimeId);
            Assert.IsTrue(left.ApplyEffect(leftSpec, new ActorId(1), bus));
            Assert.IsTrue(right.ApplyEffect(rightSpec, new ActorId(2), bus));
            Assert.AreEqual(1, leftSpec.RuntimeId);
            Assert.AreEqual(1, rightSpec.RuntimeId);

            var template = new DashAbility("dash", (FP)1, (FP)1, FixedConfig.FromConfig(0.2f), null);
            var abilityDef = new GameplayAbilityDef(template);
            var first = abilityDef.CreateAbility();
            var second = abilityDef.CreateAbility();
            Assert.AreNotSame(template, first);
            Assert.AreNotSame(first, second);
        }

        [Test]
        public void ResetForReuseRestoresSpawnAttack()
        {
            var asc = new AbilitySystemComponent(new ActorId(3));
            asc.InitializeCombatAttributes((FP)40, (FP)10);
            asc.Attributes.GetOrCreate(BattleConstants.Attack, (FP)10);
            asc.CaptureSpawnBases();
            var bus = new ZeroGcEventBus();
            var instant = new GameplayEffectDef(
                "instant-atk",
                EffectDurationPolicy.Instant,
                FP.Zero,
                new[]
                {
                    new EffectModifier(BattleConstants.Attack, EffectModifierOperation.Add, 4f)
                });
            Assert.IsTrue(asc.ApplyEffect(instant, new ActorId(3), bus));
            Assert.AreEqual((FP)14, asc.Attributes.GetBaseValue(BattleConstants.Attack));

            asc.ResetForReuse(bus, (FP)55);
            Assert.AreEqual((FP)10, asc.Attributes.GetBaseValue(BattleConstants.Attack));
            Assert.AreEqual((FP)55, asc.Attributes.GetBaseValue(BattleConstants.MaxHealth));
            Assert.AreEqual((FP)55, asc.Attributes.GetCurrentValue(BattleConstants.Health));
        }
    }
}
