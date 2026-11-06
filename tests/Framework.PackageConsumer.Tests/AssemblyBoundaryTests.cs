using Framework.Battle;
using Framework.FixedMath;
using Framework.GamePlay;
using NUnit.Framework;

namespace Framework.PackageConsumer.Tests
{
    public sealed class AssemblyBoundaryTests
    {
        [Test]
        public void ConsumerUsesSeparateProductionAssemblies()
        {
            Assert.AreEqual("Framework.FixedMath", typeof(FP).Assembly.GetName().Name);
            Assert.AreEqual("Framework.Battle.Core", typeof(ActorId).Assembly.GetName().Name);
            Assert.AreEqual("Framework.GamePlay", typeof(BattleDirector).Assembly.GetName().Name);
            Assert.AreNotSame(typeof(FP).Assembly, typeof(BattleDirector).Assembly);
            Assert.AreNotSame(GetType().Assembly, typeof(BattleDirector).Assembly);
        }
    }
}
