using Framework.Core;
using Framework.Core.Snapshot;
using Framework.ECS;
using Framework.ECS.Components;
using Framework.FixedMath;
using Framework.GAS.Snapshot;
using NUnit.Framework;

namespace Framework.Tests
{
    /// <summary>
    /// 快照前进再恢复，第二次前进应得到相同指纹。
    /// 只覆盖 ECS 组件和确定性随机；GAS 与 Farseer 接触不在这条路径里。
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
        public void GasSnapshotIsExplicitlyUnsupported()
        {
            var gas = new UnsupportedGasSnapshot();
            var buffer = new SnapshotBuffer();
            Assert.Throws<System.NotSupportedException>(() => gas.Save(buffer.BeginWrite()));
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
    }
}
