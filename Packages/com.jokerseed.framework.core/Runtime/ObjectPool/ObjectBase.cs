using System;
using Framework.MemoryPool;

namespace Framework.ObjectPool
{
    /// <summary>
    /// 对象池对象基类。实例本身通常由 <see cref="MemoryPool.MemoryPool"/> 分配。
    /// 参考 TEngine ObjectBase。
    /// </summary>
    public abstract class ObjectBase : IMemory
    {
        /// <summary>对象名称。</summary>
        public string Name { get; private set; }

        /// <summary>持有的目标实例（如 GameObject）。</summary>
        public object Target { get; private set; }

        /// <summary>是否锁定（锁定时不会被自动释放）。</summary>
        public bool Locked { get; set; }

        /// <summary>优先级，数值越大越不易被优先释放。</summary>
        public int Priority { get; set; }

        /// <summary>上次使用时间（UTC）。</summary>
        public DateTime LastUseTime { get; internal set; }

        /// <summary>自定义是否可释放。</summary>
        public virtual bool CustomCanReleaseFlag => true;

        protected ObjectBase()
        {
            Name = string.Empty;
            Target = null;
            Locked = false;
            Priority = 0;
            LastUseTime = DateTime.MinValue;
        }

        /// <summary>以匿名方式初始化对象（名称为空字符串）。</summary>
        /// <param name="target">持有的目标实例，不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> 为 null。</exception>
        protected void Initialize(object target)
        {
            Initialize(null, target, false, 0);
        }

        /// <summary>以指定名称初始化对象。</summary>
        /// <param name="name">对象名称；为 null 时使用空字符串。</param>
        /// <param name="target">持有的目标实例，不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> 为 null。</exception>
        protected void Initialize(string name, object target)
        {
            Initialize(name, target, false, 0);
        }

        /// <summary>以完整参数初始化对象。</summary>
        /// <param name="name">对象名称；为 null 时使用空字符串。</param>
        /// <param name="target">持有的目标实例，不可为 null。</param>
        /// <param name="locked">是否锁定，锁定时不会被自动释放。</param>
        /// <param name="priority">优先级，数值越大越不易被优先释放。</param>
        /// <exception cref="ArgumentNullException"><paramref name="target"/> 为 null。</exception>
        protected void Initialize(string name, object target, bool locked, int priority)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            Name = name ?? string.Empty;
            Target = target;
            Locked = locked;
            Priority = priority;
            LastUseTime = DateTime.UtcNow;
        }

        /// <summary>真正销毁/清理目标时调用（从池剔除时）。</summary>
        /// <param name="isShutdown">是否因模块关闭而销毁；可据此跳过某些清理逻辑。</param>
        protected abstract void Release(bool isShutdown);

        /// <summary>
        /// 从内存池取壳并 <see cref="Initialize(string, object, bool, int)"/>；供子类工厂使用。
        /// </summary>
        /// <typeparam name="T">具体对象类型，须可无参构造。</typeparam>
        /// <param name="name">对象名称；为 null 时使用空字符串。</param>
        /// <param name="target">持有的目标实例，不可为 null。</param>
        /// <param name="locked">是否锁定。</param>
        /// <param name="priority">优先级。</param>
        /// <returns>已初始化的实例。</returns>
        protected static T CreateInstance<T>(string name, object target, bool locked = false, int priority = 0)
            where T : ObjectBase, new()
        {
            var obj = global::Framework.MemoryPool.MemoryPool.Acquire<T>();
            obj.Initialize(name, target, locked, priority);
            return obj;
        }

        /// <summary>
        /// 子类静态 <c>Spawn</c> 的推荐封装：委托给 <see cref="IObjectPool{T}.SpawnOrCreate(Func{T})"/>。
        /// </summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="pool">目标对象池，不可为 null。</param>
        /// <param name="factory">创建新实例的工厂（通常调用 <see cref="CreateInstance{T}"/>）。</param>
        /// <returns>取出或新建的实例。</returns>
        protected static T SpawnFromPool<T>(IObjectPool<T> pool, Func<T> factory) where T : ObjectBase
        {
            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            return pool.SpawnOrCreate(factory);
        }

        /// <summary>带名称匹配的 <see cref="SpawnFromPool{T}(IObjectPool{T}, Func{T})"/>。</summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="pool">目标对象池，不可为 null。</param>
        /// <param name="name">优先匹配的对象名称。</param>
        /// <param name="factory">创建新实例的工厂。</param>
        /// <returns>取出或新建的实例。</returns>
        protected static T SpawnFromPool<T>(IObjectPool<T> pool, string name, Func<T> factory) where T : ObjectBase
        {
            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            return pool.SpawnOrCreate(name, factory);
        }

        /// <summary>归还内存池前复位字段；若仍持有 Target 会先 Release。</summary>
        public void Clear()
        {
            if (Target != null)
            {
                Release(false);
            }

            Name = string.Empty;
            Target = null;
            Locked = false;
            Priority = 0;
            LastUseTime = DateTime.MinValue;
        }

        internal void InternalRelease(bool isShutdown)
        {
            Release(isShutdown);
            Target = null;
        }
    }
}
