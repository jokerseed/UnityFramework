using System;
using Framework.Battle.Snapshot;
using Framework.ECS;
using Framework.ECS.Components;
using Framework.FixedMath;
using NUnit.Framework;

namespace Framework.Tests
{
    /// <summary>实体下标回收（代数加一），以及未登记组件不能静默离开快照。</summary>
    public sealed class EcsIdTests
    {
        [Test]
        public void DestroyedEntityIndexIsReusedWithNextGeneration()
        {
            var world = new World();
            var first = world.CreateEntity();
            var second = world.CreateEntity();
            Assert.AreEqual(1u, first.Id);
            Assert.AreEqual(2u, second.Id);
            world.DestroyEntity(first);
            var reused = world.CreateEntity();
            Assert.AreEqual(1u, Entity.IndexOf(reused.Id));
            Assert.AreEqual(1u, Entity.GenerationOf(reused.Id));
            Assert.AreNotEqual(first.Id, reused.Id);
            Assert.IsFalse(first.IsAlive);
        }

        [Test]
        public void StaleIdCannotReadOrRemoveReusedEntityComponents()
        {
            var world = new World();
            var first = world.CreateEntity();
            var staleId = first.Id;
            world.DestroyEntity(first);
            var reused = world.CreateEntity();
            world.AddComponent(reused, new TransformComponent { Position = TSVector.up });

            var storage = world.GetStorage<TransformComponent>();
            Assert.IsFalse(storage.TryGet(staleId, out _));
            Assert.IsFalse(storage.Remove(staleId));
            Assert.IsFalse(world.TryGetComponent<TransformComponent>(staleId, out _));
            Assert.IsTrue(storage.TryGet(reused.Id, out var transform));
            Assert.AreEqual(TSVector.up, transform.Position);
            Assert.Throws<InvalidOperationException>(() => storage.Add(staleId, new TransformComponent()));

            world.DestroyEntity(staleId);
            Assert.IsTrue(world.IsCurrent(reused));
        }

        [Test]
        public void SaveRejectsUnregisteredCustomComponent()
        {
            var world = new World();
            var entity = world.CreateEntity();
            world.AddComponent(entity, new ProbeComponent { Value = 3 });
            var buffer = new SnapshotBuffer();
            var writer = buffer.BeginWrite();
            Assert.Throws<InvalidOperationException>(() => world.Save(writer));
        }

        [Test]
        public void FreeListRoundTripsAndStaleEntityIsIgnored()
        {
            var world = new World();
            var first = world.CreateEntity();
            var second = world.CreateEntity();
            world.DestroyEntity(first.Id);
            Assert.IsFalse(world.IsCurrent(first));

            var buffer = new SnapshotBuffer();
            world.Save(buffer.BeginWrite());
            var restored = new World();
            restored.Restore(buffer.BeginRead());
            var reused = restored.CreateEntity();
            Assert.AreEqual(Entity.IndexOf(first.Id), Entity.IndexOf(reused.Id));
            Assert.AreEqual(Entity.GenerationOf(first.Id) + 1, Entity.GenerationOf(reused.Id));
            Assert.IsTrue(restored.IsCurrent(restored.TryGetEntity(second.Id, out var kept) ? kept : null));

            world.AddComponent(first, new TransformComponent { Position = TSVector.up });
            Assert.IsFalse(world.GetStorage<TransformComponent>().TryGet(first.Id, out _));
            var fresh = world.CreateEntity();
            Assert.AreEqual(Entity.IndexOf(first.Id), Entity.IndexOf(fresh.Id));
            world.DestroyEntity(first);
            world.DestroyEntity(first.Id);
            Assert.IsTrue(world.IsCurrent(fresh));
        }

        [Test]
        public void RegisteredSnapshotExtensionRoundTrips()
        {
            var source = new World();
            source.AddSnapshotExtension(new ProbeExtension());
            var entity = source.CreateEntity();
            source.AddComponent(entity, new ProbeComponent { Value = 9 });
            var buffer = new SnapshotBuffer();
            source.Save(buffer.BeginWrite());

            var restored = new World();
            restored.AddSnapshotExtension(new ProbeExtension());
            restored.Restore(buffer.BeginRead());
            Assert.IsTrue(restored.GetStorage<ProbeComponent>().TryGet(entity.Id, out var probe));
            Assert.AreEqual(9, probe.Value);
        }

        struct ProbeComponent : IComponent
        {
            public int Value;
        }

        sealed class ProbeExtension : IEcsSnapshotExtension
        {
            public int TypeId => 100;

            public Type ComponentType => typeof(ProbeComponent);

            public void Save(World world, SnapshotWriter writer)
            {
                var storage = world.GetStorage<ProbeComponent>();
                writer.WriteInt(storage.Count);
                foreach (var pair in storage)
                {
                    writer.WriteUInt(pair.Key);
                    writer.WriteInt(pair.Value.Value);
                }
            }

            public void Restore(World world, SnapshotReader reader)
            {
                var count = reader.ReadInt();
                var storage = world.GetStorage<ProbeComponent>();
                for (var i = 0; i < count; i++)
                {
                    var id = reader.ReadUInt();
                    storage.Add(id, new ProbeComponent { Value = reader.ReadInt() });
                }
            }
        }
    }
}
