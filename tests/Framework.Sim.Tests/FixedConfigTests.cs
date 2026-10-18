using System;
using Framework.FixedMath;
using NUnit.Framework;

namespace Framework.Sim.Tests
{
    /// <summary>配置数值进入定点模拟时，float、double、decimal 写的同一个十进制数必须得到同一个 FP。</summary>
    public sealed class FixedConfigTests
    {
        [TestCase(0.1f, 0.1d, "0.1")]
        [TestCase(0.28f, 0.28d, "0.28")]
        [TestCase(1.6f, 1.6d, "1.6")]
        [TestCase(-2.5f, -2.5d, "-2.5")]
        [TestCase(123.4567f, 123.4567d, "123.4567")]
        public void SameDecimal_FromFloatDoubleAndDecimal_GivesSameFp(float single, double dbl, string text)
        {
            // float 只有 7 位有效数字，表里超过 7 位的值本身就存不下，这里只测 7 位以内的写法。
            var fromFloat = FixedConfig.FromConfig(single);
            Assert.AreEqual(fromFloat.RawValue, FixedConfig.FromConfig(dbl).RawValue);
            Assert.AreEqual(fromFloat.RawValue, FixedConfig.FromConfig(decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture)).RawValue);
        }

        [Test]
        public void TenthIsQuantizedToDecimalValue()
        {
            // 0.1 * 2^32 = 429496729.6，截断后是 429496729；0.1f 实际是 0.100000001490116…，直接转成 FP 会多出 7 个最小单位。
            Assert.AreEqual(429496729L, FixedConfig.FromConfig(0.1f).RawValue);
            Assert.AreEqual(429496736L, ((FP)0.1f).RawValue);
        }

        [Test]
        public void IntegersAndBinaryFractionsAreExact()
        {
            Assert.AreEqual(((FP)3).RawValue, FixedConfig.FromConfig(3f).RawValue);
            Assert.AreEqual(FP.Half.RawValue, FixedConfig.FromConfig(0.5f).RawValue);
            Assert.AreEqual(((FP)(-120)).RawValue, FixedConfig.FromConfig(-120d).RawValue);
        }

        [Test]
        public void InvalidValuesThrow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => FixedConfig.FromConfig(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => FixedConfig.FromConfig(float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => FixedConfig.FromConfig(3e9f));
            Assert.Throws<ArgumentOutOfRangeException>(() => FixedConfig.FromConfig(-3e9d));
            Assert.Throws<ArgumentOutOfRangeException>(() => FixedConfig.FromConfig(2147483648m));
        }
    }
}
