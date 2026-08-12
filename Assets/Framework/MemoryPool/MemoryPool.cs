using System;
using System.Collections.Generic;

namespace Framework.MemoryPool
{
    /// <summary>
    /// 轻量级内存池：复用实现 <see cref="IMemory"/> 的小对象，降低 GC。
    /// 参考 TEngine MemoryPool。
    /// </summary>
    public static class MemoryPool
    {
        static readonly Dictionary<Type, MemoryCollection> Collections = new Dictionary<Type, MemoryCollection>(32);
        static readonly object SyncRoot = new object();

        /// <summary>是否开启严格检查（重复 Release / 类型不匹配时抛异常）。</summary>
        public static bool EnableStrictCheck { get; set; }

        /// <summary>当前已创建的类型池数量。</summary>
        public static int Count
        {
            get
            {
                lock (SyncRoot)
                {
                    return Collections.Count;
                }
            }
        }

        /// <summary>从池中获取对象；池空则 new。</summary>
        /// <typeparam name="T">实现 <see cref="IMemory"/> 且可无参构造的类型。</typeparam>
        /// <returns>可复用的内存对象实例。</returns>
        public static T Acquire<T>() where T : class, IMemory, new()
        {
            return GetOrCreate(typeof(T)).Acquire<T>();
        }

        /// <summary>从池中获取指定类型对象。</summary>
        /// <param name="memoryType">目标类型，必须实现 <see cref="IMemory"/> 且不可为 null。</param>
        /// <returns>可复用的内存对象实例。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="memoryType"/> 为 null。</exception>
        /// <exception cref="ArgumentException"><paramref name="memoryType"/> 未实现 <see cref="IMemory"/>。</exception>
        public static IMemory Acquire(Type memoryType)
        {
            if (memoryType == null)
            {
                throw new ArgumentNullException(nameof(memoryType));
            }

            if (!typeof(IMemory).IsAssignableFrom(memoryType))
            {
                throw new ArgumentException($"Type '{memoryType.FullName}' does not implement IMemory.", nameof(memoryType));
            }

            return GetOrCreate(memoryType).Acquire();
        }

        /// <summary>归还对象到池；会先调用 <see cref="IMemory.Clear"/>。</summary>
        /// <param name="memory">要归还的对象；不可为 null；归还后禁止再访问。</param>
        /// <exception cref="ArgumentNullException"><paramref name="memory"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">开启严格检查且存在重复 Release。</exception>
        public static void Release(IMemory memory)
        {
            if (memory == null)
            {
                throw new ArgumentNullException(nameof(memory));
            }

            var type = memory.GetType();
            var collection = GetOrCreate(type);
            if (EnableStrictCheck && collection.UsingCount <= 0)
            {
                throw new InvalidOperationException($"Release mismatch for '{type.FullName}': no outstanding Acquire.");
            }

            collection.Release(memory);
        }

        /// <summary>预热：向池中添加指定数量的新实例。</summary>
        /// <typeparam name="T">实现 <see cref="IMemory"/> 且可无参构造的类型。</typeparam>
        /// <param name="count">要添加的实例数量；小于等于 0 时无操作。</param>
        public static void Add<T>(int count) where T : class, IMemory, new()
        {
            if (count <= 0)
            {
                return;
            }

            var collection = GetOrCreate(typeof(T));
            for (var i = 0; i < count; i++)
            {
                collection.Add(new T());
            }
        }

        /// <summary>清空所有类型池中的闲置对象。</summary>
        public static void ClearAll()
        {
            lock (SyncRoot)
            {
                foreach (var pair in Collections)
                {
                    pair.Value.RemoveAll();
                }

                Collections.Clear();
            }
        }

        /// <summary>获取各类型池的统计快照。</summary>
        /// <returns>所有已创建类型池的统计信息数组。</returns>
        public static MemoryPoolInfo[] GetAllInfos()
        {
            lock (SyncRoot)
            {
                var results = new MemoryPoolInfo[Collections.Count];
                var index = 0;
                foreach (var pair in Collections)
                {
                    var c = pair.Value;
                    results[index++] = new MemoryPoolInfo(
                        pair.Key,
                        c.UnusedCount,
                        c.UsingCount,
                        c.AcquireCount,
                        c.ReleaseCount,
                        c.AddCount,
                        c.RemoveCount);
                }

                return results;
            }
        }

        static MemoryCollection GetOrCreate(Type type)
        {
            lock (SyncRoot)
            {
                if (!Collections.TryGetValue(type, out var collection))
                {
                    collection = new MemoryCollection(type);
                    Collections.Add(type, collection);
                }

                return collection;
            }
        }
    }

    /// <summary>单个类型内存池的统计信息。</summary>
    public readonly struct MemoryPoolInfo
    {
        /// <summary>对象类型。</summary>
        public Type Type { get; }

        /// <summary>池中当前闲置（未使用）的对象数量。</summary>
        public int UnusedCount { get; }

        /// <summary>当前已被取出（使用中）的对象数量。</summary>
        public int UsingCount { get; }

        /// <summary>累计 Acquire 次数。</summary>
        public int AcquireCount { get; }

        /// <summary>累计 Release 次数。</summary>
        public int ReleaseCount { get; }

        /// <summary>累计新建对象次数（池为空时 new）。</summary>
        public int AddCount { get; }

        /// <summary>累计从池中移除的对象数量。</summary>
        public int RemoveCount { get; }

        /// <summary>构造统计信息快照。</summary>
        /// <param name="type">对象类型。</param>
        /// <param name="unusedCount">池中闲置对象数。</param>
        /// <param name="usingCount">使用中对象数。</param>
        /// <param name="acquireCount">累计 Acquire 次数。</param>
        /// <param name="releaseCount">累计 Release 次数。</param>
        /// <param name="addCount">累计新建次数。</param>
        /// <param name="removeCount">累计移除次数。</param>
        public MemoryPoolInfo(
            Type type,
            int unusedCount,
            int usingCount,
            int acquireCount,
            int releaseCount,
            int addCount,
            int removeCount)
        {
            Type = type;
            UnusedCount = unusedCount;
            UsingCount = usingCount;
            AcquireCount = acquireCount;
            ReleaseCount = releaseCount;
            AddCount = addCount;
            RemoveCount = removeCount;
        }
    }
}
