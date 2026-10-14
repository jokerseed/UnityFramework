using Framework.Battle;
using Framework.FixedMath;
using Framework.GamePlay;
using NUnit.Framework;

namespace Framework.Sim.Tests
{
    /// <summary>最小冒烟：引擎无关的模拟层能在纯 .NET 里建一场战斗并推进。</summary>
    public sealed class SmokeTests
    {
        [Test]
        public void BattleRunsWithoutUnity()
        {
            using (var director = new BattleDirector())
            {
                var session = director.CreateSession(randomSeed: 7);
                var hero = session.AllocateActorId();
                var enemy = session.AllocateActorId();
                session.Framework.CreateActor(hero, TSVector.zero, (FP)100, teamId: 0);
                session.Framework.CreateActor(enemy, new TSVector(3, 0, 0), (FP)80, teamId: 1);

                Assert.AreEqual(30, session.Step(30));
                Assert.IsTrue(session.Framework.TryGetActor(hero, out var asc));
                Assert.IsTrue(asc.Attributes.GetCurrentValue(BattleConstants.Health) > FP.Zero);
            }
        }
    }
}
