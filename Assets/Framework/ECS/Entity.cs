using System.Collections.Generic;

namespace Framework.ECS
{
    /// <summary>ECS 实体，持有唯一 ID 及存活状态。自身不存储组件数据，组件由 <see cref="ComponentStorage{T}"/> 统一管理。</summary>
    public sealed class Entity
    {
        /// <summary>实体的全局唯一数字 ID，由 <see cref="World"/> 分配，不可复用。</summary>
        public uint Id { get; }

        /// <summary>实体是否存活；由 <see cref="World.DestroyEntity(uint)"/> 置为 <c>false</c>。</summary>
        public bool IsAlive { get; internal set; } = true;

        /// <summary>创建一个具有指定 ID 的实体。</summary>
        /// <param name="id">全局唯一实体 ID，通常由 <see cref="World"/> 分配。</param>
        public Entity(uint id) => Id = id;
    }

    /// <summary>泛型组件存储，以 Entity ID 为键存储指定组件类型的值副本。</summary>
    /// <typeparam name="T">组件类型，须为实现 <see cref="IComponent"/> 的结构体。</typeparam>
    public sealed class ComponentStorage<T> : IComponentStorage where T : struct, IComponent
    {
        readonly Dictionary<uint, T> _components = new Dictionary<uint, T>();

        /// <summary>添加或覆盖指定实体的组件数据。</summary>
        /// <param name="entityId">目标实体 ID。</param>
        /// <param name="component">要存储的组件值；以 in 传递避免拷贝开销。</param>
        public void Add(uint entityId, in T component) => _components[entityId] = component;

        /// <summary>尝试获取指定实体的组件数据。</summary>
        /// <param name="entityId">目标实体 ID。</param>
        /// <param name="component">获取成功时输出组件的值副本；失败时为 <c>default</c>。</param>
        /// <returns>实体存在对应组件时返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public bool TryGet(uint entityId, out T component) => _components.TryGetValue(entityId, out component);

        /// <summary>移除指定实体的组件数据。</summary>
        /// <param name="entityId">目标实体 ID。</param>
        /// <returns>实体存在该组件并成功移除时返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public bool Remove(uint entityId) => _components.Remove(entityId);

        /// <summary>所有已存储的 (entityId, component) 键值对，可供系统遍历。</summary>
        public IEnumerable<KeyValuePair<uint, T>> All => _components;

        void IComponentStorage.Remove(uint entityId) => Remove(entityId);

        /// <summary>清空该存储中的所有组件数据。</summary>
        public void Clear() => _components.Clear();
    }
}
