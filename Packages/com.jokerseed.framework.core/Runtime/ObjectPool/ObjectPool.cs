using System;
using System.Collections.Generic;
using System.Diagnostics;
using Framework.Logging;
using Framework.MemoryPool;

namespace Framework.ObjectPool
{
    /// <summary>
    /// 对象池实现。闲置对象按名称放在各自的栈里，按名称取出为 O(1)；
    /// 过期判断使用单调时钟（<see cref="Stopwatch"/>），不受系统改时间影响。
    /// 单个对象释放时抛出的异常只记录日志，不会中断整批释放或关闭。
    /// </summary>
    sealed class ObjectPool<T> : IObjectPool<T>, ObjectPoolBaseTickable where T : ObjectBase
    {
        sealed class Entry
        {
            public T Object;
            public int SpawnCount;
            public bool InIdle;
            public bool Destroyed;
        }

        readonly List<Entry> _entries = new List<Entry>(16);
        readonly Dictionary<object, Entry> _map = new Dictionary<object, Entry>(16);
        readonly Dictionary<string, Stack<Entry>> _idleByName = new Dictionary<string, Stack<Entry>>(StringComparer.Ordinal);
        readonly List<Entry> _releaseCandidates = new List<Entry>(8);
        readonly bool _allowMultiSpawn;
        float _autoReleaseTime;
        Func<T> _factory;

        public string Name { get; }
        public Type ObjectType => typeof(T);
        public int Count => _entries.Count;
        public bool AllowMultiSpawn => _allowMultiSpawn;
        public float AutoReleaseInterval { get; set; }
        public int Priority { get; set; }

        int _capacity = int.MaxValue;
        float _expireTime = float.MaxValue;

        public int Capacity
        {
            get => _capacity;
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _capacity = value;
                Release();
            }
        }

        public float ExpireTime
        {
            get => _expireTime;
            set
            {
                if (value < 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _expireTime = value;
                Release();
            }
        }

        public int CanReleaseCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _entries.Count; i++)
                {
                    if (CanRelease(_entries[i]))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public ObjectPool(
            string name,
            bool allowMultiSpawn,
            float autoReleaseInterval,
            int capacity,
            float expireTime,
            int priority)
        {
            Name = name ?? string.Empty;
            _allowMultiSpawn = allowMultiSpawn;
            AutoReleaseInterval = autoReleaseInterval;
            Capacity = capacity;
            ExpireTime = expireTime;
            Priority = priority;
            _autoReleaseTime = 0f;
        }

        public void Register(T obj, bool spawned)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            if (obj.Target == null)
            {
                throw new ArgumentException("Object Target is null.", nameof(obj));
            }

            if (_map.ContainsKey(obj.Target))
            {
                throw new InvalidOperationException("Object target already registered.");
            }

            var entry = new Entry
            {
                Object = obj,
                SpawnCount = spawned ? 1 : 0,
            };
            _entries.Add(entry);
            _map.Add(obj.Target, entry);
            if (!spawned)
            {
                PushIdle(entry);
            }

            if (Count > Capacity)
            {
                Release();
            }
        }

        public bool CanSpawn() => CanSpawn(null);

        public bool CanSpawn(string name)
        {
            return PeekIdle(name) != null || (_allowMultiSpawn && FindAny(name) != null);
        }

        public T Spawn() => Spawn(null);

        public T Spawn(string name)
        {
            var entry = PopIdle(name);
            if (entry == null && _allowMultiSpawn)
            {
                entry = FindAny(name);
            }

            if (entry == null)
            {
                return null;
            }

            entry.SpawnCount++;
            entry.Object.Touch();
            try
            {
                entry.Object.OnSpawn();
            }
            catch (Exception ex)
            {
                GameLog.Exception(LogCategories.ObjectPool, ex, $"OnSpawn failed in pool '{Name}'.");
            }

            return entry.Object;
        }

        public T SpawnOrCreate(Func<T> factory) => SpawnOrCreate(null, factory);

        public T SpawnOrCreate(string name, Func<T> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            var existing = Spawn(name);
            if (existing != null)
            {
                return existing;
            }

            if (Count >= Capacity)
            {
                // 满员时先挤掉闲置对象；在用对象不会被释放
                Release(Count - Capacity + 1);
            }

            var created = factory();
            if (created == null)
            {
                throw new InvalidOperationException("SpawnOrCreate factory returned null.");
            }

            Register(created, spawned: true);
            return created;
        }

        public T SpawnOrCreate()
        {
            if (_factory == null)
            {
                throw new InvalidOperationException(
                    $"Object pool '{Name}' has no factory. Call SetFactory or use SpawnOrCreate(Func<T>).");
            }

            return SpawnOrCreate(_factory);
        }

        public void SetFactory(Func<T> factory)
        {
            _factory = factory;
        }

        public void Unspawn(T obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            if (obj.Target == null || !_map.TryGetValue(obj.Target, out var entry))
            {
                throw new InvalidOperationException("Object is not in this pool.");
            }

            if (entry.SpawnCount <= 0)
            {
                throw new InvalidOperationException("Object spawn count is already zero.");
            }

            entry.SpawnCount--;
            entry.Object.Touch();
            try
            {
                entry.Object.OnUnspawn();
            }
            catch (Exception ex)
            {
                GameLog.Exception(LogCategories.ObjectPool, ex, $"OnUnspawn failed in pool '{Name}'.");
            }

            if (entry.SpawnCount == 0)
            {
                PushIdle(entry);
            }

            if (Count > Capacity)
            {
                Release();
            }
        }

        public void Release()
        {
            var overflow = Count - Capacity;
            Release(overflow > 0 ? overflow : 0);
        }

        public void Release(int toReleaseCount)
        {
            if (toReleaseCount < 0)
            {
                return;
            }

            CollectReleaseCandidates(forceExpire: false);
            var n = toReleaseCount == 0
                ? _releaseCandidates.Count // 仅释放已过期对象
                : Math.Min(toReleaseCount, _releaseCandidates.Count);
            for (var i = 0; i < n; i++)
            {
                DestroyEntry(_releaseCandidates[i]);
            }

            _releaseCandidates.Clear();
        }

        public void ReleaseAllUnused()
        {
            CollectReleaseCandidates(forceExpire: true);
            for (var i = 0; i < _releaseCandidates.Count; i++)
            {
                DestroyEntry(_releaseCandidates[i]);
            }

            _releaseCandidates.Clear();
        }

        public void Update(float elapseSeconds)
        {
            _autoReleaseTime += elapseSeconds;
            if (_autoReleaseTime < AutoReleaseInterval)
            {
                return;
            }

            _autoReleaseTime = 0f;
            Release();
        }

        public void Shutdown()
        {
            for (var i = _entries.Count - 1; i >= 0; i--)
            {
                DestroyEntry(_entries[i], isShutdown: true);
            }

            _entries.Clear();
            _map.Clear();
            _idleByName.Clear();
        }

        static string KeyOf(ObjectBase obj) => obj.Name ?? string.Empty;

        static bool IsIdle(Entry entry) => entry.InIdle && !entry.Destroyed && entry.SpawnCount == 0;

        void PushIdle(Entry entry)
        {
            if (entry.InIdle || entry.Destroyed)
            {
                return;
            }

            var key = KeyOf(entry.Object);
            if (!_idleByName.TryGetValue(key, out var stack))
            {
                stack = new Stack<Entry>(4);
                _idleByName[key] = stack;
            }

            entry.InIdle = true;
            stack.Push(entry);
        }

        /// <summary>弹出一个闲置对象；栈里已销毁或已被取出的条目顺手丢掉。</summary>
        Entry PopIdle(string name)
        {
            if (name != null)
            {
                return _idleByName.TryGetValue(name, out var stack) ? PopValid(stack) : null;
            }

            foreach (var pair in _idleByName)
            {
                var entry = PopValid(pair.Value);
                if (entry != null)
                {
                    return entry;
                }
            }

            return null;
        }

        Entry PeekIdle(string name)
        {
            if (name != null)
            {
                return _idleByName.TryGetValue(name, out var stack) ? PeekValid(stack) : null;
            }

            foreach (var pair in _idleByName)
            {
                var entry = PeekValid(pair.Value);
                if (entry != null)
                {
                    return entry;
                }
            }

            return null;
        }

        static Entry PopValid(Stack<Entry> stack)
        {
            var entry = PeekValid(stack);
            if (entry != null)
            {
                stack.Pop();
                entry.InIdle = false;
            }

            return entry;
        }

        static Entry PeekValid(Stack<Entry> stack)
        {
            while (stack.Count > 0)
            {
                var top = stack.Peek();
                if (IsIdle(top))
                {
                    return top;
                }

                stack.Pop();
                top.InIdle = false;
            }

            return null;
        }

        /// <summary>允许多次取出时，按名称找任意一个对象（可能已被取出）。</summary>
        Entry FindAny(string name)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (name == null || string.Equals(entry.Object.Name, name, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        static bool CanRelease(Entry entry)
        {
            return entry.SpawnCount <= 0
                   && !entry.Object.Locked
                   && entry.Object.CustomCanReleaseFlag;
        }

        void CollectReleaseCandidates(bool forceExpire)
        {
            _releaseCandidates.Clear();
            var now = Stopwatch.GetTimestamp();
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!CanRelease(entry))
                {
                    continue;
                }

                if (!forceExpire)
                {
                    var unusedSeconds = (double)(now - entry.Object.LastUseTimestamp) / Stopwatch.Frequency;
                    if (unusedSeconds < ExpireTime && Count <= Capacity)
                    {
                        continue;
                    }
                }

                _releaseCandidates.Add(entry);
            }

            _releaseCandidates.Sort((a, b) =>
            {
                var priorityCmp = a.Object.Priority.CompareTo(b.Object.Priority);
                if (priorityCmp != 0)
                {
                    return priorityCmp;
                }

                return a.Object.LastUseTimestamp.CompareTo(b.Object.LastUseTimestamp);
            });
        }

        void DestroyEntry(Entry entry, bool isShutdown = false)
        {
            entry.Destroyed = true;
            _entries.Remove(entry);
            if (entry.Object.Target != null)
            {
                _map.Remove(entry.Object.Target);
            }

            try
            {
                entry.Object.InternalRelease(isShutdown);
            }
            catch (Exception ex)
            {
                GameLog.Exception(LogCategories.ObjectPool, ex, $"Release object failed in pool '{Name}'.");
            }

            try
            {
                global::Framework.MemoryPool.MemoryPool.Release(entry.Object);
            }
            catch (Exception ex)
            {
                GameLog.Exception(LogCategories.ObjectPool, ex, $"Return object to memory pool failed in pool '{Name}'.");
            }
        }
    }
}
