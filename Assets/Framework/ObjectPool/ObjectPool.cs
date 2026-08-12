using System;
using System.Collections.Generic;
using Framework.MemoryPool;

namespace Framework.ObjectPool
{
    /// <summary>对象池实现。</summary>
sealed class ObjectPool<T> : IObjectPool<T>, ObjectPoolBaseTickable where T : ObjectBase
{
        sealed class Entry
        {
            public T Object;
            public int SpawnCount;
        }

        readonly List<Entry> _entries = new List<Entry>(16);
        readonly Dictionary<object, Entry> _map = new Dictionary<object, Entry>(16);
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
            if (Count > Capacity)
            {
                Release();
            }
        }

        public bool CanSpawn() => CanSpawn(null);

        public bool CanSpawn(string name)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!NameMatches(entry.Object, name))
                {
                    continue;
                }

                if (_allowMultiSpawn || entry.SpawnCount <= 0)
                {
                    return true;
                }
            }

            return false;
        }

        public T Spawn() => Spawn(null);

        public T Spawn(string name)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!NameMatches(entry.Object, name))
                {
                    continue;
                }

                if (!_allowMultiSpawn && entry.SpawnCount > 0)
                {
                    continue;
                }

                entry.SpawnCount++;
                entry.Object.LastUseTime = DateTime.UtcNow;
                return entry.Object;
            }

            return null;
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
            entry.Object.LastUseTime = DateTime.UtcNow;
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
            if (toReleaseCount == 0)
            {
                // 仅释放已过期对象
                for (var i = 0; i < _releaseCandidates.Count; i++)
                {
                    DestroyEntry(_releaseCandidates[i]);
                }
            }
            else
            {
                var n = Math.Min(toReleaseCount, _releaseCandidates.Count);
                for (var i = 0; i < n; i++)
                {
                    DestroyEntry(_releaseCandidates[i]);
                }
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
        }

        static bool NameMatches(ObjectBase obj, string name)
        {
            if (name == null)
            {
                return true;
            }

            return string.Equals(obj.Name, name, StringComparison.Ordinal);
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
            var now = DateTime.UtcNow;
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                if (!CanRelease(entry))
                {
                    continue;
                }

                if (!forceExpire)
                {
                    var unusedSeconds = (float)(now - entry.Object.LastUseTime).TotalSeconds;
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

                return a.Object.LastUseTime.CompareTo(b.Object.LastUseTime);
            });
        }

        void DestroyEntry(Entry entry, bool isShutdown = false)
        {
            _entries.Remove(entry);
            if (entry.Object.Target != null)
            {
                _map.Remove(entry.Object.Target);
            }

            entry.Object.InternalRelease(isShutdown);
            global::Framework.MemoryPool.MemoryPool.Release(entry.Object);
        }
    }
}
