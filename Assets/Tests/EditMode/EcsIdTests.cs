using System;
using Framework.Core.Snapshot;
using Framework.ECS;
using NUnit.Framework;

namespace Framework.Tests
{
    /// <summary>实体 ID 回收，以及未登记组件不能静默离开快照。</summary>
    public sealed class EcsIdTests
    {
        [Test]
        public void DestroyedEntityIdIsReused()
        {
            var world = new World();
            var first = world.CreateEntity();
            var second = world.CreateEntity();
            Assert.AreEqual(1u, first.Id);
            Assert.AreEqual(2u, second.Id);
            world.DestroyEntity(first);
            var reused = world.CreateEntity();
            Assert.AreEqual(1u, reused.Id);
            Assert.IsFalse(first.IsAlive);
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

        struct ProbeComponent : IComponent
        {
            public int Value;
        }
    }
}
