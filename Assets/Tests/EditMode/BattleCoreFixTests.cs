using System.Collections.Generic;
using Framework.Core;
using Framework.ECS;
using Framework.ECS.Components;
using Framework.FixedMath;
using Framework.GamePlay;
using NUnit.Framework;
using UnityEngine;

namespace Framework.Tests
{
    /// <summary>刚体落点、扇形夹角、第 0 帧物理克隆，以及销毁实体时卸刚体。</summary>
    public sealed class BattleCoreFixTests
    {
        [Test]
        public void ActorBodyKeepsNonZeroPositionAfterOneStep()
        {
            var physics = new BattlePhysicsWorld();
            var start = new TSVector((FP)3, FP.Zero, (FP)4);
            physics.AddActorBody(1, new ActorId(1), 0, start, FP.Half);
            physics.Step(FP.EN2);

            var transform = new TransformComponent { Position = start };
            Assert.IsTrue(physics.TryWriteTransform(1, ref transform));
            Assert.AreEqual(start.x, transform.Position.x);
            Assert.AreEqual(start.z, transform.Position.z);
        }

        [Test]
        public void ConeQueryHitsTheTargetStraightAhead()
        {
            using (var framework = new GamePlayFramework(1))
            {
                var hero = new ActorId(1);
                var ahead = new ActorId(2);
                var side = new ActorId(3);
                framework.CreateActor(hero, Vector3.zero, 100f, teamId: 0);
                framework.CreateActor(ahead, new Vector3(0f, 0f, 2f), 100f, teamId: 1);
                framework.CreateActor(side, new Vector3(2f, 0f, 0f), 100f, teamId: 1);
                framework.Tick(FP.Zero);

                var results = new List<ActorId>();
                framework.QueryEnemiesInCone(
                    hero,
                    TSVector.zero,
                    TSVector.forward,
                    (FP)60,
                    (FP)5,
                    results);
                Assert.AreEqual(1, results.Count);
                Assert.AreEqual(ahead, results[0]);
            }
        }

        [Test]
        public void SaveFullKeepsBodiesThatHaveNotBeenStepped()
        {
            var physics = new BattlePhysicsWorld();
            physics.AddActorBody(1, new ActorId(1), 0, new TSVector(FP.One, FP.Zero, FP.Zero), FP.Half);
            var snapshot = new BattlePhysicsSnapshot();
            physics.SaveFull(snapshot);
            physics.AddActorBody(2, new ActorId(2), 1, new TSVector((FP)3, FP.Zero, FP.Zero), FP.Half);
            physics.Step(FP.EN2);
            physics.RestoreFull(snapshot);
            physics.Step(FP.EN2);

            var transform = new TransformComponent();
            Assert.IsTrue(physics.TryWriteTransform(1, ref transform));
            Assert.AreEqual(FP.One, transform.Position.x);
            Assert.IsFalse(physics.TryWriteTransform(2, ref transform));
        }

        [Test]
        public void DestroyEntityRemovesBodyThroughIEcsBodyRemover()
        {
            var world = new World();
            var physics = new BattlePhysicsWorld();
            world.RegisterSingleton(physics);
            world.RegisterSingleton<IEcsBodyRemover>(physics);
            var entity = world.CreateEntity();
            physics.AddActorBody(entity.Id, new ActorId(1), 0, new TSVector((FP)2, FP.Zero, FP.Zero), FP.Half);

            var transform = new TransformComponent();
            Assert.IsTrue(physics.TryWriteTransform(entity.Id, ref transform));
            world.DestroyEntity(entity.Id);
            Assert.IsFalse(physics.TryWriteTransform(entity.Id, ref transform));
        }
    }
}
