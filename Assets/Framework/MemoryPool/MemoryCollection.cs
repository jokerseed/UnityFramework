using System;
using System.Collections.Generic;

namespace Framework.MemoryPool
{
    /// <summary>单类型内存集合（内部）。</summary>
    sealed class MemoryCollection
    {
        readonly Queue<IMemory> _unused = new Queue<IMemory>(8);
        readonly Type _memoryType;

        public int UnusedCount => _unused.Count;
        public int UsingCount { get; private set; }
        public int AcquireCount { get; private set; }
        public int ReleaseCount { get; private set; }
        public int AddCount { get; private set; }
        public int RemoveCount { get; private set; }

        public MemoryCollection(Type memoryType)
        {
            _memoryType = memoryType;
        }

        public T Acquire<T>() where T : class, IMemory, new()
        {
            AcquireCount++;
            T memory;
            if (_unused.Count > 0)
            {
                memory = (T)_unused.Dequeue();
            }
            else
            {
                memory = new T();
                AddCount++;
            }

            UsingCount++;
            return memory;
        }

        public IMemory Acquire()
        {
            AcquireCount++;
            IMemory memory;
            if (_unused.Count > 0)
            {
                memory = _unused.Dequeue();
            }
            else
            {
                memory = (IMemory)Activator.CreateInstance(_memoryType);
                AddCount++;
            }

            UsingCount++;
            return memory;
        }

        public void Release(IMemory memory)
        {
            memory.Clear();
            _unused.Enqueue(memory);
            ReleaseCount++;
            UsingCount--;
        }

        public void Add(IMemory memory)
        {
            _unused.Enqueue(memory);
            AddCount++;
        }

        public void RemoveAll()
        {
            RemoveCount += _unused.Count;
            _unused.Clear();
            UsingCount = 0;
        }
    }
}
