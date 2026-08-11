using System.Collections.Generic;

namespace Framework.ECS
{
    public sealed class Entity
    {
        public uint Id { get; }
        public bool IsAlive { get; internal set; } = true;

        public Entity(uint id) => Id = id;
    }

    public sealed class ComponentStorage<T> : IComponentStorage where T : struct, IComponent
    {
        readonly Dictionary<uint, T> _components = new Dictionary<uint, T>();

        public void Add(uint entityId, in T component) => _components[entityId] = component;

        public bool TryGet(uint entityId, out T component) => _components.TryGetValue(entityId, out component);

        public bool Remove(uint entityId) => _components.Remove(entityId);

        public IEnumerable<KeyValuePair<uint, T>> All => _components;

        void IComponentStorage.Remove(uint entityId) => Remove(entityId);

        public void Clear() => _components.Clear();
    }
}
