using System;
using System.Collections.Generic;

namespace Framework.ECS
{
    /// <summary>ECS 实体，持有唯一 ID 及存活状态。自身不存储组件数据，组件由 <see cref="ComponentStorage{T}"/> 统一管理。</summary>
    public sealed class Entity
    {
        /// <summary>ID 低位里下标占的位数。</summary>
        public const int IndexBits = 20;

        /// <summary>下标掩码；下标从 1 开始，0 保留为无效 ID。</summary>
        public const uint IndexMask = (1u << IndexBits) - 1;

        /// <summary>代数上限。代数用到这个值的下标不再复用，避免绕回后旧 ID 重新指向新实体。</summary>
        public const uint MaxGeneration = (1u << (32 - IndexBits)) - 1;

        /// <summary>
        /// 实体 ID，由 <see cref="World"/> 分配：低 <see cref="IndexBits"/> 位是下标，高位是代数。
        /// 销毁后下标会复用给新实体，但代数加一，所以旧 ID 查不到新实体的组件，旧的 <see cref="Entity"/> 实例也不会复活。
        /// </summary>
        public uint Id { get; }

        /// <summary>取 ID 里的下标部分。</summary>
        /// <param name="entityId">实体 ID。</param>
        /// <returns>下标。</returns>
        public static uint IndexOf(uint entityId) => entityId & IndexMask;

        /// <summary>取 ID 里的代数部分。</summary>
        /// <param name="entityId">实体 ID。</param>
        /// <returns>代数；从未复用过的下标为 0。</returns>
        public static uint GenerationOf(uint entityId) => entityId >> IndexBits;

        /// <summary>用下标和代数拼出实体 ID。</summary>
        /// <param name="index">下标，1 到 <see cref="IndexMask"/>。</param>
        /// <param name="generation">代数，0 到 <see cref="MaxGeneration"/>。</param>
        /// <returns>实体 ID。</returns>
        public static uint MakeId(uint index, uint generation) => (generation << IndexBits) | (index & IndexMask);

        /// <summary>实体是否存活；由 <see cref="World.DestroyEntity(uint)"/> 置为 <c>false</c>。</summary>
        public bool IsAlive { get; internal set; } = true;

        /// <summary>创建一个具有指定 ID 的实体。</summary>
        /// <param name="id">全局唯一实体 ID，通常由 <see cref="World"/> 分配。</param>
        public Entity(uint id) => Id = id;
    }

    /// <summary>
    /// 泛型组件存储。稠密数组按实体下标升序存放，稀疏表用下标直接定位，再核对完整 ID：
    /// 下标被新实体复用后，拿着旧 ID 查不到、也删不掉新实体的组件。
    /// 迭代不装箱，顺序稳定为下标升序。插入和删除为 O(数量)，适合单场战斗的实体规模。
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

        /// <summary>添加或覆盖指定实体的组件数据，并保持下标升序。</summary>
        /// <param name="entityId">目标实体 ID。</param>
        /// <param name="component">要存储的组件值；以 in 传递避免拷贝开销。</param>
        /// <exception cref="InvalidOperationException">同一下标上已有另一代实体的组件，说明调用方拿的是过期 ID。</exception>
        public void Add(uint entityId, in T component)
        {
            var slot = Entity.IndexOf(entityId);
            EnsureSparse(slot);
            var existing = _sparse[slot];
            if (existing >= 0)
            {
                if (_denseIds[existing] != entityId)
                {
                    throw new InvalidOperationException(
                        "Entity " + entityId + " is stale: index " + slot + " now belongs to entity " + _denseIds[existing] + ".");
                }

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
            while (insertAt > 0 && Entity.IndexOf(_denseIds[insertAt - 1]) > slot)
            {
                var shifted = _denseIds[insertAt - 1];
                _denseIds[insertAt] = shifted;
                _dense[insertAt] = _dense[insertAt - 1];
                _sparse[Entity.IndexOf(shifted)] = insertAt;
                insertAt--;
            }

            _denseIds[insertAt] = entityId;
            _dense[insertAt] = component;
            _sparse[slot] = insertAt;
            _count++;
        }

        /// <summary>尝试获取指定实体的组件数据。</summary>
        /// <param name="entityId">目标实体 ID。</param>
        /// <param name="component">获取成功时输出组件的值副本；失败时为 <c>default</c>。</param>
        /// <returns>实体存在对应组件时返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        public bool TryGet(uint entityId, out T component)
        {
            var index = Find(entityId);
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
            var index = Find(entityId);
            if (index < 0)
            {
                return false;
            }

            _sparse[Entity.IndexOf(entityId)] = -1;
            var last = _count - 1;
            for (var i = index; i < last; i++)
            {
                var shifted = _denseIds[i + 1];
                _denseIds[i] = shifted;
                _dense[i] = _dense[i + 1];
                _sparse[Entity.IndexOf(shifted)] = i;
            }

            _dense[last] = default;
            _denseIds[last] = 0;
            _count = last;
            return true;
        }

        /// <summary>
        /// 全部 (entityId, component) 对，按实体下标升序。foreach 使用结构体枚举器，不装箱。
        /// </summary>
        public PairEnumerable All => new PairEnumerable(this);

        /// <summary>按实体下标升序枚举，供 foreach 使用，不装箱。</summary>
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
            /// <returns>按实体下标升序的枚举器。</returns>
            public Enumerator GetEnumerator() => new Enumerator(_storage);
        }

        /// <summary>按实体下标升序遍历的结构体枚举器。</summary>
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
                var slot = Entity.IndexOf(_denseIds[i]);
                if (slot < (uint)_sparse.Length)
                {
                    _sparse[slot] = -1;
                }

                _dense[i] = default;
                _denseIds[i] = 0;
            }

            _count = 0;
        }

        /// <summary>按完整 ID 找稠密下标；下标空着或被另一代实体占用时返回 -1。</summary>
        int Find(uint entityId)
        {
            var slot = Entity.IndexOf(entityId);
            if (slot >= (uint)_sparse.Length)
            {
                return -1;
            }

            var index = _sparse[slot];
            return index >= 0 && _denseIds[index] == entityId ? index : -1;
        }

        void EnsureSparse(uint slot)
        {
            var needed = (int)slot + 1;
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
