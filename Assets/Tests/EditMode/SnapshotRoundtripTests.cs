using System;
using System.Collections.Generic;
using Framework.Battle;
using Framework.Battle.Snapshot;
using Framework.ECS;
using Framework.ECS.Components;
using Framework.FixedMath;
using Framework.GamePlay;
using Framework.GAS;
using Framework.GAS.Abilities;
using Framework.GAS.Abilities.Builtin;
using Framework.GAS.Effects;
using Framework.GAS.Snapshot;
using Framework.GAS.Tags;
using NUnit.Framework;
using UnityEngine;

namespace Framework.Tests
{
    /// <summary>
    /// 快照前进再恢复，第二次前进应得到相同指纹。
    /// ECS、随机数、GAS 和 Farseer 接触克隆都走这条路径。接触克隆不是字节流。
    /// </summary>
    public sealed class SnapshotRoundtripTests
    {
        [Test]
        public void ComponentStorageIteratesByEntityId()
        {
            var world = new World();
            var first = world.CreateEntity();
            var second = world.CreateEntity();
            var third = world.CreateEntity();
            world.AddComponent(third, new TeamComponent { TeamId = 3 });
            world.AddComponent(first, new TeamComponent { TeamId = 1 });
            world.AddComponent(second, new TeamComponent { TeamId = 2 });

            var ids = new uint[3];
            var index = 0;
            foreach (var pair in world.GetStorage<TeamComponent>())
            {
                ids[index++] = pair.Key;
            }

            Assert.AreEqual(first.Id, ids[0]);
            Assert.AreEqual(second.Id, ids[1]);
            Assert.AreEqual(third.Id, ids[2]);
            world.Dispose();
        }

        [Test]
        public void RestoreThenAdvanceMatchesOriginalAdvance()
        {
            var random = TSRandom.New(5);
            var world = new World();
            var entity = world.CreateEntity();
            world.AddComponent(entity, new TransformComponent
            {
                Position = new TSVector(FP.One, FP.Zero, (FP)2),
                Forward = TSVector.forward
            });
            world.AddComponent(entity, new VelocityComponent
            {
                Value = new TSVector((FP)1.5f, FP.Zero, (FP)(-0.25f))
            });
            world.AddComponent(entity, new ProjectileComponent
            {
                Owner = new ActorId(4),
                AbilityId = "fireball",
                Damage = (FP)3,
                RemainingLifetime = FP.One,
                TeamId = 1,
                PierceRemaining = 2,
                HitEffectId = "burn",
                ExplodeRadius = FP.Half,
                DamageType = BattleDamageType.Magical
            });

            var randomSnapshot = new DeterministicRandomSnapshot(random);
            var buffer = new SnapshotBuffer();
            var writer = buffer.BeginWrite();
            randomSnapshot.Save(writer);
            world.Save(writer);

            var before = Fingerprint(world, random);
            Advance(world, random);
            var after = Fingerprint(world, random);
            Assert.AreNotEqual(before, after);

            var reader = buffer.BeginRead();
            randomSnapshot.Restore(reader);
            world.Restore(reader);
            Assert.AreEqual(0, reader.Remaining);
            Assert.AreEqual(before, Fingerprint(world, random));

            Advance(world, random);
            Assert.AreEqual(after, Fingerprint(world, random));
            world.Dispose();
        }

        [Test]
        public void PhysicsKinematicsRoundTrip()
        {
            var physics = new BattlePhysicsWorld();
            physics.AddActorBody(3, new ActorId(9), 1, new TSVector(FP.One, FP.Zero, FP.One), FP.Half);
            physics.SetLinearVelocity(3, new TSVector((FP)2, FP.Zero, (FP)(-1)));

            var saved = new SnapshotBuffer();
            physics.SaveKinematics(saved.BeginWrite());
            physics.SetLinearVelocity(3, TSVector.zero);
            physics.SetPosition(3, new TSVector((FP)8, FP.Zero, FP.Zero));
            physics.RestoreKinematics(saved.BeginRead());

            var again = new SnapshotBuffer();
            physics.SaveKinematics(again.BeginWrite());
            Assert.AreEqual(saved.Length, again.Length);
            physics.Clear();
        }

        [Test]
        public void FullPhysicsSnapshotMatchesLaterSteps()
        {
            var physics = new BattlePhysicsWorld();
            physics.AddActorBody(1, new ActorId(1), 0, TSVector.zero, FP.Half);
            physics.AddActorBody(2, new ActorId(2), 1, new TSVector((FP)0.2f, FP.Zero, FP.Zero), FP.Half);
            physics.SetLinearVelocity(1, new TSVector((FP)2, FP.Zero, FP.Zero));
            physics.SetLinearVelocity(2, new TSVector((FP)(-2), FP.Zero, FP.Zero));

            var step = (FP)1 / 30;
            for (var i = 0; i < 8; i++)
            {
                physics.Step(step);
            }

            var snapshot = new BattlePhysicsSnapshot();
            physics.SaveFull(snapshot);
            var touch = new List<uint>(4);
            var atSnapshot = HashPhysics(physics, touch);

            const int steps = 12;
            var forward = new long[steps];
            for (var i = 0; i < steps; i++)
            {
                physics.Step(step);
                forward[i] = HashPhysics(physics, touch);
            }

            Assert.AreNotEqual(atSnapshot, forward[steps - 1]);
            physics.RestoreFull(snapshot);
            Assert.AreEqual(atSnapshot, HashPhysics(physics, touch));
            for (var i = 0; i < steps; i++)
            {
                physics.Step(step);
                Assert.AreEqual(forward[i], HashPhysics(physics, touch), "step " + i);
            }

            physics.Clear();
        }

        static long HashPhysics(BattlePhysicsWorld physics, List<uint> touch)
        {
            unchecked
            {
                long hash = 17;
                hash = HashBody(hash, physics, 1, touch);
                hash = HashBody(hash, physics, 2, touch);
                return hash;
            }
        }

        static long HashBody(long hash, BattlePhysicsWorld physics, uint entityId, List<uint> touch)
        {
            var transform = default(TransformComponent);
            physics.TryWriteTransform(entityId, ref transform);
            hash = (hash * 31) + transform.Position.x.RawValue;
            hash = (hash * 31) + transform.Position.z.RawValue;
            physics.CollectTouchingEntityIds(entityId, touch);
            hash = (hash * 31) + touch.Count;
            for (var i = 0; i < touch.Count; i++)
            {
                hash = (hash * 31) + touch[i];
            }

            return hash;
        }

        [Test]
        public void GasSnapshotAdvanceRestoreMatchesChecksum()
        {
            const int warmup = 4;
            const int steps = 20;
            using (var director = new BattleDirector())
            {
                var session = director.CreateSession(11);
                var framework = session.Framework;
                var hero = session.AllocateActorId();
                var enemy = session.AllocateActorId();
                framework.CreateActor(hero, Vector3.zero, 200f, 0);
                framework.CreateActor(enemy, new Vector3(0f, 0f, 1.2f), 80f, 1);
                Assert.IsTrue(framework.TryGetActor(hero, out var heroAsc));
                Assert.IsTrue(framework.TryGetActor(enemy, out var enemyAsc));

                var sweep = new MeleeSweepAbility(
                    "sweep",
                    5f,
                    12f,
                    2f,
                    60f,
                    framework.QueryEnemiesInCone,
                    0.2f,
                    0.2f,
                    0.3f);
                var specHandle = heroAsc.GiveAbility(new GameplayAbilityDef(sweep));
                var buff = new GameplayEffectDef(
                    "Buff.Strike",
                    EffectDurationPolicy.Duration,
                    (FP)0.5f,
                    new[]
                    {
                        new EffectModifier(BattleConstants.Attack, EffectModifierOperation.Add, 5f),
                        new EffectModifier(BattleConstants.Shield, EffectModifierOperation.Add, 4f)
                    },
                    new[] { new GameplayTag("Buff.Strike") },
                    EffectStackingPolicy.StackCount,
                    maxStacks: 3);
                Assert.IsTrue(heroAsc.ApplyEffect(buff, hero, framework.EventBus, null, heroAsc));
                Assert.IsTrue(heroAsc.ApplyEffect(buff, hero, framework.EventBus, null, heroAsc));
                heroAsc.StartCooldown("fireball", (FP)2, framework.EventBus);
                var activated = heroAsc.TryActivateAbility(
                    specHandle,
                    new AbilityActivationContext(TSVector.zero, TSVector.forward, enemy, (FP)2),
                    framework.Context,
                    out _);
                Assert.IsTrue(activated.Success, activated.FailureReason.ToString());
                session.Step(warmup);

                var components = CollectAsc(framework);
                var gas = new GasSnapshot(components, framework.Context);
                var randomSnapshot = new DeterministicRandomSnapshot(framework.Random);
                var buffer = new SnapshotBuffer();
                var writer = buffer.BeginWrite();
                randomSnapshot.Save(writer);
                framework.EcsWorld.Save(writer);
                framework.EcsWorld.GetSingleton<BattlePhysicsWorld>().SaveKinematics(writer);
                gas.Save(writer);

                var atSnapshot = BattleFrameChecksum.Compute(framework);
                var attack = heroAsc.Attributes.GetCurrentValue(BattleConstants.Attack).RawValue;
                var shield = heroAsc.Attributes.GetCurrentValue(BattleConstants.Shield).RawValue;
                var effectHandle = heroAsc.GetActiveEffect(0).Handle.Value;
                var stacks = heroAsc.GetActiveEffect(0).StackCount;
                var enemyHealth = enemyAsc.Attributes.GetCurrentValue(BattleConstants.Health).RawValue;
                Assert.AreEqual(specHandle.Value, heroAsc.ActiveInstances[0].Spec.Handle.Value);
                Assert.Greater(heroAsc.ActiveEffectCount, 1);

                var forward = new long[steps];
                for (var i = 0; i < steps; i++)
                {
                    session.Step(1);
                    forward[i] = BattleFrameChecksum.Compute(framework);
                }

                Assert.AreNotEqual(atSnapshot, forward[steps - 1]);
                Assert.Less(enemyAsc.Attributes.GetCurrentValue(BattleConstants.Health).RawValue, enemyHealth);

                var reader = buffer.BeginRead();
                randomSnapshot.Restore(reader);
                framework.EcsWorld.Restore(reader);
                framework.EcsWorld.GetSingleton<BattlePhysicsWorld>().RestoreKinematics(reader);
                gas.Restore(reader);
                Assert.AreEqual(0, reader.Remaining);
                framework.Commands.ClearAll();
                framework.Registry.RebindEntitiesAfterSnapshot();
                framework.Registry.SyncPositionsFromEcs();
                framework.EcsWorld.GetSingleton<SpatialHashGrid>().MarkStructuralDirty();

                Assert.AreEqual(atSnapshot, BattleFrameChecksum.Compute(framework));
                Assert.AreEqual(attack, heroAsc.Attributes.GetCurrentValue(BattleConstants.Attack).RawValue);
                Assert.AreEqual(shield, heroAsc.Attributes.GetCurrentValue(BattleConstants.Shield).RawValue);
                Assert.AreEqual(effectHandle, heroAsc.GetActiveEffect(0).Handle.Value);
                Assert.AreEqual(stacks, heroAsc.GetActiveEffect(0).StackCount);
                Assert.AreEqual(1, heroAsc.ActiveInstances.Count);
                Assert.AreEqual(specHandle.Value, heroAsc.ActiveInstances[0].Spec.Handle.Value);
                Assert.AreEqual((int)ActiveAbilityState.Active, (int)heroAsc.ActiveInstances[0].State);

                for (var i = 0; i < steps; i++)
                {
                    session.Step(1);
                    Assert.AreEqual(forward[i], BattleFrameChecksum.Compute(framework), "step " + i);
                }

                var next = heroAsc.GiveAbility(new GameplayAbilityDef(new DashAbility("dash", 1f, 1f, 0.2f, null)));
                Assert.AreEqual(specHandle.Value + 1, next.Value);
            }
        }

        static List<AbilitySystemComponent> CollectAsc(GamePlayFramework framework)
        {
            var ids = new List<ActorId>();
            framework.Registry.CopySortedActorIds(ids);
            var list = new List<AbilitySystemComponent>(ids.Count);
            for (var i = 0; i < ids.Count; i++)
            {
                Assert.IsTrue(framework.TryGetActor(ids[i], out var asc));
                list.Add(asc);
            }

            return list;
        }

        static void Advance(World world, TSRandom random)
        {
            var transforms = world.GetStorage<TransformComponent>();
            var velocities = world.GetStorage<VelocityComponent>();
            foreach (var pair in transforms)
            {
                if (!velocities.TryGet(pair.Key, out var velocity))
                {
                    continue;
                }

                var jitter = (FP)random.Next(0, 4) * FP.EN2;
                var position = pair.Value.Position;
                var step = velocity.Value;
                transforms.Add(pair.Key, new TransformComponent
                {
                    Position = new TSVector(
                        position.x + step.x * FP.EN1 + jitter,
                        position.y,
                        position.z + step.z * FP.EN1),
                    Forward = pair.Value.Forward
                });
            }
        }

        static long Fingerprint(World world, TSRandom random)
        {
            unchecked
            {
                long hash = random.GetStateHash();
                foreach (var pair in world.GetStorage<TransformComponent>())
                {
                    hash = (hash * 31) + pair.Key;
                    hash = (hash * 31) + pair.Value.Position.x.RawValue;
                    hash = (hash * 31) + pair.Value.Position.y.RawValue;
                    hash = (hash * 31) + pair.Value.Position.z.RawValue;
                }

                foreach (var pair in world.GetStorage<ProjectileComponent>())
                {
                    hash = (hash * 31) + pair.Value.PierceRemaining;
                    hash = (hash * 31) + pair.Value.RemainingLifetime.RawValue;
                    hash = (hash * 31) + pair.Value.TeamId;
                }

                return hash;
            }
        }

        [Test]
        public void DefaultReaderHasNoBytesAndRejectsReads()
        {
            var reader = default(SnapshotReader);
            Assert.AreEqual(0, reader.Remaining);
            Assert.Throws<InvalidOperationException>(() => reader.ReadInt());
        }

        [Test]
        public void BeginReadRestartsAndOversizedStringLengthThrows()
        {
            var buffer = new SnapshotBuffer();
            var writer = buffer.BeginWrite();
            writer.WriteString("ab");
            var first = buffer.BeginRead();
            Assert.AreEqual("ab", first.ReadString());
            var second = buffer.BeginRead();
            Assert.AreEqual("ab", second.ReadString());

            var bytes = new byte[8];
            bytes[0] = 0xFF;
            bytes[1] = 0xFF;
            bytes[2] = 0xFF;
            bytes[3] = 0x7F;
            var crafted = new SnapshotReader(bytes, bytes.Length);
            Assert.Throws<InvalidOperationException>(() => crafted.ReadString());
        }
    }
}
