using Framework.BehaviourTree;
using Framework.FixedMath;
using Framework.GAS.Attributes;
using Framework.GAS.Effects;
using NUnit.Framework;

namespace Framework.Tests
{
    /// <summary>阶段 3：叠层乘法和黑板恢复不要留下更新的对象。</summary>
    public sealed class BattleP1Tests
    {
        [Test]
        public void SingleStackMultiplyStaysLinearAndExtraStacksDoNotScaleTheWholeFactor()
        {
            var single = EvaluateMultiply((FP)2, 1);
            Assert.AreEqual((FP)20, single);

            var stacked = EvaluateMultiply((FP)2, 3);
            Assert.AreEqual((FP)40, stacked);

            var set = new GameplayAttributeSet();
            set.GetOrCreate("Attack", (FP)7);
            var aggregator = new AttributeModifierAggregator();
            aggregator.Add("Attack", (FP)100, EffectModifierOperation.Override, 2);
            aggregator.ApplyTo(set);
            Assert.AreEqual((FP)100, set.GetCurrentValue("Attack"));
        }

        [Test]
        public void AuthoritativeCopyClearsObjectBag()
        {
            var source = new BtBlackboard();
            var target = new BtBlackboard();
            target.SetObject("marker", "future");
            target.CopyAuthoritativeFrom(source);
            Assert.IsFalse(target.TryGet("marker", out string _));
        }

        static FP EvaluateMultiply(FP magnitude, int stacks)
        {
            var set = new GameplayAttributeSet();
            set.GetOrCreate("Attack", (FP)10);
            var aggregator = new AttributeModifierAggregator();
            aggregator.Add("Attack", magnitude, EffectModifierOperation.Multiply, stacks);
            aggregator.ApplyTo(set);
            return set.GetCurrentValue("Attack");
        }
    }
}
