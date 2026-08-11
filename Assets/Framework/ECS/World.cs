using System.Collections.Generic;
using Framework.Core;
using Framework.Core.Commands;
using Framework.Core.Events;
using Framework.Core.Tick;
using Framework.ECS.Components;

namespace Framework.ECS
{
    public sealed class World : ITickable
    {
        uint _nextEntityId = 1;
        readonly List<ISystem> _systems = new List<ISystem>();
        readonly Dictionary<uint, Entity> _entities = new Dictionary<uint, Entity>();
        readonly Dictionary<System.Type, IComponentStorage> _storages = new Dictionary<System.Type, IComponentStorage>();

        public BattleCommandBuffer Commands { get; set; }
        public object UserData { get; set; }

        public World()
        {
        }

        public Entity CreateEntity()
        {
            var entity = new Entity(_nextEntityId++);
            _entities[entity.Id] = entity;
            return entity;
        }

        public bool IsAlive(Entity entity) =>
            entity != null && entity.IsAlive && _entities.ContainsKey(entity.Id);

        public void DestroyEntity(uint entityId)
        {
            if (!_entities.TryGetValue(entityId, out var entity))
            {
                return;
            }

            entity.IsAlive = false;
            _entities.Remove(entityId);

            foreach (var storage in _storages.Values)
            {
                storage.Remove(entityId);
            }
        }

        public void DestroyEntity(Entity entity)
        {
            if (entity != null)
            {
                DestroyEntity(entity.Id);
            }
        }

        public ComponentStorage<T> GetStorage<T>() where T : struct, IComponent
        {
            var type = typeof(T);
            if (!_storages.TryGetValue(type, out var storage))
            {
                storage = new ComponentStorage<T>();
                _storages[type] = storage;
            }

            return (ComponentStorage<T>)storage;
        }

        public void AddComponent<T>(Entity entity, in T component) where T : struct, IComponent
        {
            GetStorage<T>().Add(entity.Id, component);
        }

        public bool TryGetComponent<T>(Entity entity, out T component) where T : struct, IComponent
        {
            component = default;
            return entity != null && entity.IsAlive && GetStorage<T>().TryGet(entity.Id, out component);
        }

        public void AddSystem(ISystem system)
        {
            _systems.Add(system);
            system.OnCreate(this);
        }

        public void Tick(float deltaTime)
        {
            for (var i = 0; i < _systems.Count; i++)
            {
                _systems[i].Update(this, deltaTime);
            }
        }

        public void Dispose()
        {
            for (var i = _systems.Count - 1; i >= 0; i--)
            {
                _systems[i].OnDestroy(this);
            }

            _systems.Clear();
            _entities.Clear();

            foreach (var storage in _storages.Values)
            {
                storage.Clear();
            }

            _storages.Clear();
        }
    }
}
