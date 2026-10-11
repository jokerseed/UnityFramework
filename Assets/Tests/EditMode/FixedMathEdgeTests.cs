using Framework.FixedMath;
using NUnit.Framework;

namespace Framework.Tests
{
    /// <summary>FixedMath 边界：除零、负数开方、越界反余弦、Slerp、长度截断、随机区间。</summary>
    public sealed class FixedMathEdgeTests
    {
        [Test]
        public void DivisionByZeroFollowsSignAndNormalQuotientStaysExact()
        {
            Assert.AreEqual(FP.MaxValue, FP.One / FP.Zero);
            Assert.AreEqual(FP.MinValue, ((FP)(-1)) / FP.Zero);
            Assert.AreEqual(FP.Zero, FP.Zero / FP.Zero);
            Assert.AreEqual((FP)4, (FP)8 / (FP)2);
            Assert.AreEqual(FP.Zero, (FP)5 % FP.Zero);
            Assert.AreEqual((FP)1, (FP)5 % (FP)2);
        }

        [Test]
        public void SqrtOfNegativeIsZeroAndPerfectSquareStaysExact()
        {
            Assert.AreEqual(FP.Zero, FP.Sqrt((FP)(-4)));
            Assert.AreEqual((FP)5, new TSVector(3, 4, 0).magnitude);
            Assert.DoesNotThrow(() =>
            {
                var huge = new TSVector((FP)100000, FP.Zero, FP.Zero);
                var length = huge.magnitude;
                Assert.IsTrue(length > FP.Zero);
            });
        }

        [Test]
        public void AcosClampsToUnitIntervalInRadians()
        {
            Assert.AreEqual(FP.Zero, FP.Acos((FP)2));
            Assert.AreEqual(FP.Pi, FP.Acos((FP)(-2)));
            Assert.AreEqual(FP.Zero, FP.Acos(FP.One));
            Assert.AreEqual(FP.Pi, FP.Acos(-FP.One));
            Assert.AreEqual(FP.PiOver2, FP.Acos(FP.Zero));
        }

        [Test]
        public void SlerpOfEqualQuaternionsIsNotZero()
        {
            var identity = new TSQuaternion(FP.Zero, FP.Zero, FP.Zero, FP.One);
            var result = TSQuaternion.Slerp(identity, identity, FP.Half);
            Assert.AreEqual(FP.Zero, result.x);
            Assert.AreEqual(FP.Zero, result.y);
            Assert.AreEqual(FP.Zero, result.z);
            Assert.AreEqual(FP.One, result.w);
            Assert.AreEqual(FP.Zero, TSQuaternion.Angle(identity, identity));
        }

        [Test]
        public void OppositeVectorsProduceAHalfTurn()
        {
            var turn = TSQuaternion.FromToRotation(TSVector.up, TSVector.down);
            Assert.AreEqual(FP.Zero, turn.w);
            Assert.IsTrue(turn.x * turn.x + turn.y * turn.y + turn.z * turn.z > FP.Zero);
        }

        [Test]
        public void ClampMagnitudeLeavesShorterVectorsUntouched()
        {
            var shortVector = new TSVector(1, 0, 0);
            var kept = TSVector.ClampMagnitude(shortVector, (FP)5);
            Assert.AreEqual(shortVector.x, kept.x);
            Assert.AreEqual(shortVector.y, kept.y);
            Assert.AreEqual(shortVector.z, kept.z);

            var longVector = new TSVector(5, 0, 0);
            var clamped = TSVector.ClampMagnitude(longVector, FP.One);
            Assert.AreEqual(FP.One, clamped.magnitude);
            Assert.AreEqual(TSVector.zero, TSVector.ClampMagnitude(TSVector.zero, (FP)3));
        }

        [Test]
        public void NextWithEqualBoundsDoesNotThrowAndOtherRangesStayRepeatable()
        {
            var once = TSRandom.New(1);
            Assert.AreEqual(4, once.Next(4, 4));

            var left = TSRandom.New(7);
            var right = TSRandom.New(7);
            for (var i = 0; i < 16; i++)
            {
                Assert.AreEqual(left.Next(0, 10), right.Next(0, 10));
            }
        }
    }
}
