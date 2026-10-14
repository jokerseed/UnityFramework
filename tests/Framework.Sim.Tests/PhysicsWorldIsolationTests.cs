using Framework.FixedMath;
using Framework.Lockstep.Physics2D;
using NUnit.Framework;

namespace Framework.Sim.Tests
{
    /// <summary>
    /// Body / Fixture ID 按世界独立计数：同进程先后或同时存在的多个世界（多局、回放对拍）编号互不影响。
    /// </summary>
    public sealed class PhysicsWorldIsolationTests
    {
        [Test]
        public void BodyAndFixtureIdsArePerWorld()
        {
            var first = new World(TSVector2.zero);
            var a0 = BodyFactory.CreateCircle(first, FP.One, FP.One);
            var a1 = BodyFactory.CreateCircle(first, FP.One, FP.One);

            var second = new World(TSVector2.zero);
            var b0 = BodyFactory.CreateCircle(second, FP.One, FP.One);

            Assert.AreEqual(0, a0.BodyId);
            Assert.AreEqual(1, a1.BodyId);
            Assert.AreEqual(0, b0.BodyId, "新世界的 Body ID 应从 0 开始，不受其它世界影响。");
            Assert.AreEqual(a0.FixtureList[0].FixtureId, b0.FixtureList[0].FixtureId);
        }
    }
}
