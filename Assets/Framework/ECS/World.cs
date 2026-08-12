using System.Collections.Generic;
using Framework.Core;
using Framework.Core.Commands;
using Framework.Core.Tick;
using Framework.ECS.Components;

namespace Framework.ECS
{
    /// <summary>
    /// ECS 世界，负责实体的生命周期、组件存储和系统调度。
    /// 每个战斗实例对应一个 World，由 <see cref="Framework.GamePlay.GamePlayFramework"/> 持有。
    /// </summary>
    public sealed class World : ITickable
    {
        uint _nextEntityId = 1;
        readonly List<ISystem> _systems = new List<ISystem>();
        readonly Dictionary<uint, Entity> _entities = new Dictionary<uint, Entity>();
        readonly Dictionary<System.Type, IComponentStorage> _storages = new Dictionary<System.Type, IComponentStorage>();

        /// <summary>本帧命令缓冲，系统可向其写入伤害/生成等延迟指令；由外部注入。</summary>
        public BattleCommandBuffer Commands { get; set; }

        /// <summary>用户自定义数据槽，供系统间共享任意对象（如 <see cref="SpatialHashGrid"/>）；不由 World 管理其生命周期。</summary>
        public object UserData { get; set; }

        /// <summary>创建一个空的 ECS 世界。</summary>
        public World()
        {
        }

        /// <summary>创建新实体并返回其引用，实体 ID 自增且不复用。</summary>
        /// <returns>新建的 <see cref="Entity"/> 实例，初始状态为存活。</returns>
        public Entity CreateEntity()
        {
            var entity = new Entity(_nextEntityId++);
            _entities[entity.Id] = entity;
            return entity;
        }

        /// <summary>判断实体是否仍在世界中存活。</summary>
        /// <param name="entity">要检查的实体；可为 null。</param>
        /// <returns>实体非 null、标记存活且仍在世界字典中时返回 <c>true</c>。</returns>
        public bool IsAlive(Entity entity) =>
            entity != null && entity.IsAlive && _entities.ContainsKey(entity.Id);

        /// <summary>按 ID 销毁实体，同时从所有组件存储中移除其数据。</summary>
        /// <param name="entityId">要销毁的实体 ID；若不存在则静默忽略。</param>
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

        /// <summary>销毁实体，同时从所有组件存储中移除其数据。</summary>
        /// <param name="entity">要销毁的实体；若为 null 则静默忽略。</param>
        public void DestroyEntity(Entity entity)
        {
            if (entity != null)
            {
                DestroyEntity(entity.Id);
            }
        }

        /// <summary>获取指定组件类型的存储，不存在时自动创建。</summary>
        /// <typeparam name="T">组件类型，须为实现 <see cref="IComponent"/> 的结构体。</typeparam>
        /// <returns>对应类型的 <see cref="ComponentStorage{T}"/> 实例。</returns>
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

        /// <summary>为实体添加或覆盖指定类型的组件数据。</summary>
        /// <typeparam name="T">组件类型，须为实现 <see cref="IComponent"/> 的结构体。</typeparam>
        /// <param name="entity">目标实体；不可为 null。</param>
        /// <param name="component">要写入的组件值；以 in 传递避免拷贝开销。</param>
        public void AddComponent<T>(Entity entity, in T component) where T : struct, IComponent
        {
            GetStorage<T>().Add(entity.Id, component);
        }

        /// <summary>尝试获取实体的指定组件数据。</summary>
        /// <typeparam name="T">组件类型，须为实现 <see cref="IComponent"/> 的结构体。</typeparam>
        /// <param name="entity">目标实体；可为 null。</param>
        /// <param name="component">获取成功时输出组件的值副本；失败时为 <c>default</c>。</param>
        /// <returns>实体存活且拥有该组件时返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public bool TryGetComponent<T>(Entity entity, out T component) where T : struct, IComponent
        {
            component = default;
            return entity != null && entity.IsAlive && GetStorage<T>().TryGet(entity.Id, out component);
        }

        /// <summary>向世界注册系统，并立即调用其 <see cref="ISystem.OnCreate"/>。</summary>
        /// <param name="system">要注册的系统实例；不可为 null。</param>
        public void AddSystem(ISystem system)
        {
            _systems.Add(system);
            system.OnCreate(this);
        }

        /// <summary>按注册顺序依次驱动所有系统执行一帧逻辑。</summary>
        /// <param name="deltaTime">距上一帧的时间间隔（秒）。</param>
        public void Tick(float deltaTime)
        {
            for (var i = 0; i < _systems.Count; i++)
            {
                _systems[i].Update(this, deltaTime);
            }
        }

        /// <summary>销毁世界：逆序调用所有系统的 <see cref="ISystem.OnDestroy"/>，并清空全部存储。</summary>
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
