using System;
using System.Collections.Generic;

namespace Framework.MemoryPool
{
    /// <summary>
    /// 单类型内存集合（内部）。Acquire / Release 与计数共用一把锁，供主线程和网络 IO 线程同时访问。
    /// <see cref="IMemory.Clear"/> 在锁内调用，实现里不要再进入同一个类型池。
    /// </summary>
    sealed class MemoryCollection
    {
        readonly object _gate = new object();
        readonly Queue<IMemory> _unused = new Queue<IMemory>(8);
        readonly Type _memoryType;
        int _usingCount;
        int _acquireCount;
        int _releaseCount;
        int _addCount;
        int _removeCount;

        public int UnusedCount
        {
            get
            {
                lock (_gate)
                {
                    return _unused.Count;
                }
            }
        }

        public int UsingCount
        {
            get
            {
                lock (_gate)
                {
                    return _usingCount;
                }
            }
        }

        public int AcquireCount
        {
            get
            {
                lock (_gate)
                {
                    return _acquireCount;
                }
            }
        }

        public int ReleaseCount
        {
            get
            {
                lock (_gate)
                {
                    return _releaseCount;
                }
            }
        }

        public int AddCount
        {
            get
            {
                lock (_gate)
                {
                    return _addCount;
                }
            }
        }

        public int RemoveCount
        {
            get
            {
                lock (_gate)
                {
                    return _removeCount;
                }
            }
        }

        public MemoryCollection(Type memoryType)
        {
            _memoryType = memoryType;
        }

        public T Acquire<T>() where T : class, IMemory, new()
        {
            lock (_gate)
            {
                _acquireCount++;
                T memory;
                if (_unused.Count > 0)
                {
                    memory = (T)_unused.Dequeue();
                }
                else
                {
                    memory = new T();
                    _addCount++;
                }

                _usingCount++;
                return memory;
            }
        }

        public IMemory Acquire()
        {
            lock (_gate)
            {
                _acquireCount++;
                IMemory memory;
                if (_unused.Count > 0)
                {
                    memory = _unused.Dequeue();
                }
                else
                {
                    memory = (IMemory)Activator.CreateInstance(_memoryType);
                    _addCount++;
                }

                _usingCount++;
                return memory;
            }
        }

        /// <summary>归还对象。严格检查与入队在同一把锁里完成。</summary>
        /// <param name="memory">要归还的对象。</param>
        /// <param name="strict">为 true 且没有在用对象时抛异常。</param>
        /// <exception cref="InvalidOperationException">严格检查发现没有对应的 Acquire。</exception>
        public void Release(IMemory memory, bool strict)
        {
            lock (_gate)
            {
                if (strict && _usingCount <= 0)
                {
                    throw new InvalidOperationException(
                        $"Release mismatch for '{_memoryType.FullName}': no outstanding Acquire.");
                }

                memory.Clear();
                _unused.Enqueue(memory);
                _releaseCount++;
                _usingCount--;
            }
        }

        public void Add(IMemory memory)
        {
            lock (_gate)
            {
                _unused.Enqueue(memory);
                _addCount++;
            }
        }

        public void RemoveAll()
        {
            lock (_gate)
            {
                _removeCount += _unused.Count;
                _unused.Clear();
                _usingCount = 0;
            }
        }
    }
}
