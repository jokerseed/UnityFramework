using Framework.Battle;
using Framework.FixedMath;
using Framework.GamePlay;
using Framework.GAS.Abilities;
using Framework.GAS.Abilities.Builtin;
using NUnit.Framework;

namespace Framework.Sim.Tests
{
    /// <summary>投射物一步飞过的距离大于目标直径时，扫掠检测仍要命中。</summary>
    public sealed class ProjectileSweepTests
    {
        const string AbilityId = "Test.Railgun";

        [Test]
        public void FastProjectile_HitsTargetItWouldOtherwiseTunnelThrough()
        {
            using (var director = new BattleDirector())
            {
                var session = director.CreateSession(1);
                var framework = session.Framework;
                var hero = session.AllocateActorId();
                var enemy = session.AllocateActorId();
                framework.CreateActor(hero, TSVector.zero, (FP)100, teamId: 0);
                framework.CreateActor(enemy, new TSVector(3, 0, 0), (FP)80, teamId: 1);

                // 30Hz 下每步 5 米：第一步后投射物在 x=5，目标在 x=3、半径 0.5，只看接触会穿过去。
                framework.GiveAbility(hero, new GameplayAbilityDef(new ProjectileAbility(
                    AbilityId,
                    cooldown: (FP)10,
                    speed: (FP)150,
                    radius: FixedConfig.FromConfig(0.1f),
                    lifetime: FP.One,
                    damage: (FP)10,
                    teamId: 0)));

                var cast = false;
                session.Step(1, _ =>
                {
                    var result = framework.TryActivateAbility(
                        hero,
                        AbilityId,
                        new AbilityActivationContext(TSVector.zero, new TSVector(1, 0, 0), enemy, (FP)20));
                    cast = result.Success;
                });
                session.Step(2);

                Assert.IsTrue(cast, "projectile ability did not activate");
                Assert.IsTrue(framework.TryGetActor(enemy, out var enemyAsc));
                Assert.Less(enemyAsc.Attributes.GetCurrentValue(BattleConstants.Health).RawValue, ((FP)80).RawValue);
            }
        }
    }
}
