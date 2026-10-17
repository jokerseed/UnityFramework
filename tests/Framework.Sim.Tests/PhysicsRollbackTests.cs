using System.Collections.Generic;
using Framework.Battle;
using Framework.ECS;
using Framework.ECS.Components;
using Framework.FixedMath;
using Framework.GamePlay;
using Framework.GAS.Abilities;
using Framework.GAS.Abilities.Builtin;
using NUnit.Framework;

namespace Framework.Sim.Tests
{
    /// <summary>快照之后停用或删除的刚体，回滚后必须照常碰撞。</summary>
    public sealed class PhysicsRollbackTests
    {
        static readonly FP Dt = (FP)1 / 30;

        [Test]
        public void DisabledBody_CollidesAgainAfterRestoreAndCanBeToggled()
        {
            var physics = new BattlePhysicsWorld();
            physics.AddActorBody(1, new ActorId(1), 0, TSVector.zero, FP.Half);
            physics.AddActorBody(2, new ActorId(2), 1, new TSVector(FixedConfig.FromConfig(0.6f), FP.Zero, FP.Zero), FP.Half);
            var touching = new List<uint>();
            physics.Step(Dt);
            physics.CollectTouchingEntityIds(1, touching);
            CollectionAssert.Contains(touching, 2u);

            var snapshot = new BattlePhysicsSnapshot();
            physics.SaveFull(snapshot);
            physics.SetEnabled(2, false);
            physics.Step(Dt);
            physics.CollectTouchingEntityIds(1, touching);
            CollectionAssert.DoesNotContain(touching, 2u);

            // 恢复后刚体回到启用状态，Fixture 的代理表要跟着重建，否则下一步查重叠会用到已释放的代理号。
            physics.RestoreFull(snapshot);
            physics.Step(Dt);
            physics.CollectTouchingEntityIds(1, touching);
            CollectionAssert.Contains(touching, 2u);

            physics.SetEnabled(2, false);
            physics.Step(Dt);
            physics.CollectTouchingEntityIds(1, touching);
            CollectionAssert.DoesNotContain(touching, 2u);

            // 重新启用后新接触在这一步里才建立，下一步才算接触。
            physics.SetEnabled(2, true);
            physics.Step(Dt);
            physics.Step(Dt);
            physics.CollectTouchingEntityIds(1, touching);
            CollectionAssert.Contains(touching, 2u);
        }

        [Test]
        public void RetainedBody_ComesBackAfterRestoreAndIsPurgedLater()
        {
            var physics = new BattlePhysicsWorld { RetainRemovedBodies = true };
            physics.AddActorBody(1, new ActorId(1), 0, new TSVector(-5, 0, 0), FP.Half);
            physics.AddActorBody(2, new ActorId(2), 1, TSVector.zero, FP.Half);
            physics.AddProjectileBody(3, new ActorId(1), 0, new TSVector(FixedConfig.FromConfig(0.3f), FP.Zero, FP.Zero), FixedConfig.FromConfig(0.2f));
            var touching = new List<uint>();
            physics.Step(Dt);
            physics.CollectTouchingEntityIds(3, touching);
            CollectionAssert.Contains(touching, 2u);

            var beforeRemove = new BattlePhysicsSnapshot();
            physics.SaveFull(beforeRemove);
            physics.RemoveBody(3);
            Assert.AreEqual(1, physics.RetiredBodyCount);
            Assert.AreEqual(2, physics.ManagedBodyCount);
            physics.Step(Dt);

            physics.RestoreFull(beforeRemove);
            Assert.AreEqual(0, physics.RetiredBodyCount);
            Assert.AreEqual(3, physics.ManagedBodyCount);
            physics.Step(Dt);
            physics.CollectTouchingEntityIds(3, touching);
            CollectionAssert.Contains(touching, 2u);

            physics.RemoveBody(3);
            physics.Step(Dt);
            var afterRemove = new BattlePhysicsSnapshot();
            physics.SaveFull(afterRemove);
            physics.PurgeRetiredBefore(afterRemove.StepCount);
            Assert.AreEqual(0, physics.RetiredBodyCount);
            Assert.AreEqual(2, physics.WorldBodyCount);

            physics.Step(Dt);
            physics.RestoreFull(afterRemove);
            Assert.AreEqual(2, physics.ManagedBodyCount);
            physics.Step(Dt);
            physics.CollectTouchingEntityIds(1, touching);
            Assert.IsEmpty(touching);
        }

        [Test]
        public void ProjectileDestroyedAfterCheckpoint_ReplaysToSameChecksums()
        {
            using (var sim = new BattleLockstepSimulation(11))
            {
                var framework = sim.Session.Framework;
                var actors = new List<ActorId>();
                framework.Registry.CopySortedActorIds(actors);
                framework.GiveAbility(actors[0], new GameplayAbilityDef(new ProjectileAbility(
                    "Test.Bolt",
                    cooldown: (FP)10,
                    speed: (FP)6,
                    radius: FixedConfig.FromConfig(0.2f),
                    lifetime: (FP)2,
                    damage: (FP)5,
                    teamId: 0)));

                var players = new[] { 1, 2 };
                var idle = new[] { BattleLockstepSimulation.EncodeVelocity(0, 0), BattleLockstepSimulation.EncodeVelocity(0, 0) };
                Assert.IsTrue(framework.TryActivateAbility(
                    actors[0],
                    "Test.Bolt",
                    new AbilityActivationContext(TSVector.zero, new TSVector(1, 0, 0), actors[1], (FP)10)).Success);
                for (var tick = 0; tick < 3; tick++)
                {
                    sim.Simulate(tick, players, idle);
                }

                var projectiles = framework.EcsWorld.GetStorage<ProjectileComponent>();
                Assert.AreEqual(1, projectiles.Count, "projectile should be in flight at the checkpoint");
                sim.SaveCheckpoint(3);
                var expected = new long[30];
                for (var tick = 3; tick < 30; tick++)
                {
                    sim.Simulate(tick, players, idle);
                    expected[tick] = sim.Checksum;
                }

                Assert.AreEqual(0, projectiles.Count, "projectile should have hit and been destroyed");
                sim.RestoreCheckpoint(3);
                Assert.AreEqual(1, projectiles.Count);
                for (var tick = 3; tick < 30; tick++)
                {
                    sim.Simulate(tick, players, idle);
                    Assert.AreEqual(expected[tick], sim.Checksum, "tick " + tick);
                }
            }
        }
    }
}
