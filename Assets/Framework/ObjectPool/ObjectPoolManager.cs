using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Logging;
using UnityEngine;

namespace Framework.ObjectPool
{
    /// <summary>
    /// 对象池管理器：创建/查找命名池，每帧自动释放过期对象。
    /// </summary>
    public sealed class ObjectPoolManager : PersistentSingleton<ObjectPoolManager>
    {
        readonly Dictionary<string, object> _pools = new Dictionary<string, object>(16);

        /// <summary>当前池数量。</summary>
        public int Count => _pools.Count;

        void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>驱动各池的自动释放逻辑。</summary>
        /// <param name="deltaTime">本帧经过的时间（秒）。</param>
        public void Tick(float deltaTime)
        {
            foreach (var pair in _pools)
            {
                if (pair.Value is ObjectPoolBaseTickable tickable)
                {
                    tickable.Update(deltaTime);
                }
            }
        }

        /// <summary>创建单次 Spawn 池（同一对象未归还前不能再次取出）。</summary>
        /// <typeparam name="T">池内对象类型，须继承 <see cref="ObjectBase"/>。</typeparam>
        /// <param name="name">池名称，为 null 时使用空字符串；同类型不同名视为不同池。</param>
        /// <param name="autoReleaseInterval">自动释放检查间隔（秒），默认 60 秒。</param>
        /// <param name="capacity">容量上限，默认不限制。</param>
        /// <param name="expireTime">未使用对象过期时间（秒），默认不过期。</param>
        /// <param name="priority">池优先级，默认 0。</param>
        /// <returns>创建好的对象池接口。</returns>
        /// <exception cref="InvalidOperationException">同类型同名的池已存在。</exception>
        public IObjectPool<T> CreateSingleSpawnObjectPool<T>(
            string name = null,
            float autoReleaseInterval = 60f,
            int capacity = int.MaxValue,
            float expireTime = float.MaxValue,
            int priority = 0) where T : ObjectBase
        {
            return CreatePool<T>(name, allowMultiSpawn: false, autoReleaseInterval, capacity, expireTime, priority);
        }

        /// <summary>创建多次 Spawn 池（引用计数式复用）。</summary>
        /// <typeparam name="T">池内对象类型，须继承 <see cref="ObjectBase"/>。</typeparam>
        /// <param name="name">池名称，为 null 时使用空字符串；同类型不同名视为不同池。</param>
        /// <param name="autoReleaseInterval">自动释放检查间隔（秒），默认 60 秒。</param>
        /// <param name="capacity">容量上限，默认不限制。</param>
        /// <param name="expireTime">未使用对象过期时间（秒），默认不过期。</param>
        /// <param name="priority">池优先级，默认 0。</param>
        /// <returns>创建好的对象池接口。</returns>
        /// <exception cref="InvalidOperationException">同类型同名的池已存在。</exception>
        public IObjectPool<T> CreateMultiSpawnObjectPool<T>(
            string name = null,
            float autoReleaseInterval = 60f,
            int capacity = int.MaxValue,
            float expireTime = float.MaxValue,
            int priority = 0) where T : ObjectBase
        {
            return CreatePool<T>(name, allowMultiSpawn: true, autoReleaseInterval, capacity, expireTime, priority);
        }

        /// <summary>是否存在指定类型池。</summary>
        /// <typeparam name="T">池内对象类型，须继承 <see cref="ObjectBase"/>。</typeparam>
        /// <param name="name">池名称；为 null 时匹配无名称的池。</param>
        /// <returns>池存在则返回 true，否则返回 false。</returns>
        public bool HasObjectPool<T>(string name = null) where T : ObjectBase
        {
            return _pools.ContainsKey(MakeKey(typeof(T), name));
        }

        /// <summary>获取已创建的池。</summary>
        /// <typeparam name="T">池内对象类型，须继承 <see cref="ObjectBase"/>。</typeparam>
        /// <param name="name">池名称；为 null 时匹配无名称的池。</param>
        /// <returns>找到则返回对应池接口，未找到则返回 null。</returns>
        public IObjectPool<T> GetObjectPool<T>(string name = null) where T : ObjectBase
        {
            var key = MakeKey(typeof(T), name);
            if (!_pools.TryGetValue(key, out var pool))
            {
                return null;
            }

            return (IObjectPool<T>)pool;
        }

        /// <summary>销毁指定池。</summary>
        /// <typeparam name="T">池内对象类型，须继承 <see cref="ObjectBase"/>。</typeparam>
        /// <param name="name">池名称；为 null 时匹配无名称的池。</param>
        /// <returns>成功销毁返回 true，池不存在返回 false。</returns>
        public bool DestroyObjectPool<T>(string name = null) where T : ObjectBase
        {
            var key = MakeKey(typeof(T), name);
            if (!_pools.TryGetValue(key, out var pool))
            {
                return false;
            }

            ((ObjectPoolBaseTickable)pool).Shutdown();
            _pools.Remove(key);
            return true;
        }

        /// <summary>销毁全部池。</summary>
        public void DestroyAllObjectPools()
        {
            foreach (var pair in _pools)
            {
                ((ObjectPoolBaseTickable)pair.Value).Shutdown();
            }

            _pools.Clear();
        }

        IObjectPool<T> CreatePool<T>(
            string name,
            bool allowMultiSpawn,
            float autoReleaseInterval,
            int capacity,
            float expireTime,
            int priority) where T : ObjectBase
        {
            var key = MakeKey(typeof(T), name);
            if (_pools.ContainsKey(key))
            {
                throw new InvalidOperationException($"Object pool already exists: {key}");
            }

            var pool = new ObjectPool<T>(name, allowMultiSpawn, autoReleaseInterval, capacity, expireTime, priority);
            _pools.Add(key, pool);
            GameLog.Info(LogCategories.ObjectPool, $"Created pool {LogStyle.Name(key)}  multi={LogStyle.Value(allowMultiSpawn)}");
            return pool;
        }

        static string MakeKey(Type type, string name)
        {
            name = name ?? string.Empty;
            return string.IsNullOrEmpty(name) ? type.FullName : $"{type.FullName}@{name}";
        }
    }

    /// <summary>供 Manager Tick/Shutdown 的内部约定。</summary>
    interface ObjectPoolBaseTickable
    {
        void Update(float elapseSeconds);
        void Shutdown();
    }
}
