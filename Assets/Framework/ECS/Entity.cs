using System.Collections.Generic;

namespace Framework.ECS
{
    /// <summary>ECS 实体，持有唯一 ID 及存活状态。自身不存储组件数据，组件由 <see cref="ComponentStorage{T}"/> 统一管理。</summary>
    public sealed class Entity
    {
        /// <summary>实体的数字 ID，由 <see cref="World"/> 分配。销毁后可以复用给新实体，旧的 <see cref="Entity"/> 实例不会复活。</summary>
        public uint Id { get; }

        /// <summary>实体是否存活；由 <see cref="World.DestroyEntity(uint)"/> 置为 <c>false</c>。</summary>
        public bool IsAlive { get; internal set; } = true;

        /// <summary>创建一个具有指定 ID 的实体。</summary>
        /// <param name="id">全局唯一实体 ID，通常由 <see cref="World"/> 分配。</param>
        public Entity(uint id) => Id = id;
    }

    /// <summary>
    /// 泛型组件存储。稠密数组按实体 ID 升序存放，稀疏表用实体 ID 直接定位。
    /// 迭代不装箱，顺序稳定为实体 ID 升序。插入和删除为 O(数量)，适合单场战斗的实体规模。
    /// </summary>
    /// <typeparam name="T">组件类型，须为实现 <see cref="IComponent"/> 的结构体。</typeparam>
    public sealed class ComponentStorage<T> : IComponentStorage where T : struct, IComponent
    {
        const int InitialCapacity = 16;

        uint[] _denseIds = new uint[InitialCapacity];
        T[] _dense = new T[InitialCapacity];
        int[] _sparse;
        int _count;

        /// <summary>创建空存储。</summary>
        public ComponentStorage()
        {
            _sparse = new int[InitialCapacity];
            for (var i = 0; i < _sparse.Length; i++)
            {
                _sparse[i] = -1;
            }
        }

        /// <summary>当前组件数量。</summary>
        public int Count => _count;

        /// <summary>添加或覆盖指定实体的组件数据，并保持实体 ID 升序。</summary>
        /// <param name="entityId">目标实体 ID。</param>
        /// <param name="component">要存储的组件值；以 in 传递避免拷贝开销。</param>
        public void Add(uint entityId, in T component)
        {
            EnsureSparse(entityId);
            var existing = _sparse[entityId];
            if (existing >= 0)
            {
                _dense[existing] = component;
                return;
            }

            if (_count == _denseIds.Length)
            {
                var next = _denseIds.Length * 2;
                System.Array.Resize(ref _denseIds, next);
                System.Array.Resize(ref _dense, next);
            }

            var insertAt = _count;
            while (insertAt > 0 && _denseIds[insertAt - 1] > entityId)
            {
                var shifted = _denseIds[insertAt - 1];
                _denseIds[insertAt] = shifted;
                _dense[insertAt] = _dense[insertAt - 1];
                _sparse[shifted] = insertAt;
                insertAt--;
            }

            _denseIds[insertAt] = entityId;
            _dense[insertAt] = component;
            _sparse[entityId] = insertAt;
            _count++;
        }

        /// <summary>尝试获取指定实体的组件数据。</summary>
        /// <param name="entityId">目标实体 ID。</param>
        /// <param name="component">获取成功时输出组件的值副本；失败时为 <c>default</c>。</param>
        /// <returns>实体存在对应组件时返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public bool TryGet(uint entityId, out T component)
        {
            if (entityId >= (uint)_sparse.Length)
            {
                component = default;
                return false;
            }

            var index = _sparse[entityId];
            if (index < 0)
            {
                component = default;
                return false;
            }

            component = _dense[index];
            return true;
        }

        /// <summary>移除指定实体的组件数据。</summary>
        /// <param name="entityId">目标实体 ID。</param>
        /// <returns>实体存在该组件并成功移除时返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public bool Remove(uint entityId)
        {
            if (entityId >= (uint)_sparse.Length)
            {
                return false;
            }

            var index = _sparse[entityId];
            if (index < 0)
            {
                return false;
            }

            _sparse[entityId] = -1;
            var last = _count - 1;
            for (var i = index; i < last; i++)
            {
                var shifted = _denseIds[i + 1];
                _denseIds[i] = shifted;
                _dense[i] = _dense[i + 1];
                _sparse[shifted] = i;
            }

            _dense[last] = default;
            _denseIds[last] = 0;
            _count = last;
            return true;
        }

        /// <summary>
        /// 全部 (entityId, component) 对，按实体 ID 升序。foreach 使用结构体枚举器，不装箱。
        /// </summary>
        public PairEnumerable All => new PairEnumerable(this);

        /// <summary>按实体 ID 升序枚举，供 foreach 使用，不装箱。</summary>
        /// <returns>结构体枚举器。</returns>
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <summary>不装箱的键值枚举包装。</summary>
        public readonly struct PairEnumerable
        {
            readonly ComponentStorage<T> _storage;

            /// <summary>包装存储。</summary>
            /// <param name="storage">组件存储。</param>
            public PairEnumerable(ComponentStorage<T> storage) => _storage = storage;

            /// <summary>返回结构体枚举器。</summary>
            /// <returns>按实体 ID 升序的枚举器。</returns>
            public Enumerator GetEnumerator() => new Enumerator(_storage);
        }

        /// <summary>按实体 ID 升序遍历的结构体枚举器。</summary>
        public struct Enumerator
        {
            readonly ComponentStorage<T> _storage;
            int _index;

            /// <summary>绑定存储，游标位于第一个元素之前。</summary>
            /// <param name="storage">组件存储。</param>
            public Enumerator(ComponentStorage<T> storage)
            {
                _storage = storage;
                _index = -1;
                Current = default;
            }

            /// <summary>当前键值对。</summary>
            public KeyValuePair<uint, T> Current { get; private set; }

            /// <summary>前进到下一项。</summary>
            /// <returns>仍有元素时返回 true。</returns>
            public bool MoveNext()
            {
                if (_storage == null)
                {
                    return false;
                }

                _index++;
                if (_index >= _storage._count)
                {
                    return false;
                }

                Current = new KeyValuePair<uint, T>(_storage._denseIds[_index], _storage._dense[_index]);
                return true;
            }
        }

        void IComponentStorage.Remove(uint entityId) => Remove(entityId);

        /// <summary>清空该存储中的所有组件数据。</summary>
        public void Clear()
        {
            for (var i = 0; i < _count; i++)
            {
                var id = _denseIds[i];
                if (id < (uint)_sparse.Length)
                {
                    _sparse[id] = -1;
                }

                _dense[i] = default;
                _denseIds[i] = 0;
            }

            _count = 0;
        }

        void EnsureSparse(uint entityId)
        {
            var needed = (int)entityId + 1;
            if (needed <= _sparse.Length)
            {
                return;
            }

            var size = _sparse.Length;
            while (size < needed)
            {
                size *= 2;
            }

            var grown = new int[size];
            System.Array.Copy(_sparse, grown, _sparse.Length);
            for (var i = _sparse.Length; i < grown.Length; i++)
            {
                grown[i] = -1;
            }

            _sparse = grown;
        }
    }
}
