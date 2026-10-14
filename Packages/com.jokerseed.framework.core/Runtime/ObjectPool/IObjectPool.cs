using System;

namespace Framework.ObjectPool
{
    /// <summary>单个类型的对象池接口。</summary>
    public interface IObjectPool<T> where T : ObjectBase
    {
        /// <summary>池名称。</summary>
        string Name { get; }

        /// <summary>对象类型。</summary>
        Type ObjectType { get; }

        /// <summary>池内对象总数。</summary>
        int Count { get; }

        /// <summary>当前可释放（未使用）数量。</summary>
        int CanReleaseCount { get; }

        /// <summary>是否允许多次 Spawn 同一对象（引用计数）。</summary>
        bool AllowMultiSpawn { get; }

        /// <summary>自动释放检查间隔（秒）。</summary>
        float AutoReleaseInterval { get; set; }

        /// <summary>容量上限；超过时尝试释放未使用对象。</summary>
        int Capacity { get; set; }

        /// <summary>未使用对象过期时间（秒）。</summary>
        float ExpireTime { get; set; }

        /// <summary>池优先级。</summary>
        int Priority { get; set; }

        /// <summary>注册对象到池。</summary>
        /// <param name="obj">要注册的对象，不可为 null，且 Target 不可为 null。</param>
        /// <param name="spawned">注册后是否立即视为已被取出（SpawnCount 置为 1）。</param>
        void Register(T obj, bool spawned);

        /// <summary>是否存在可取出的对象（不指定名称）。</summary>
        /// <returns>池中有可用对象则返回 true，否则返回 false。</returns>
        bool CanSpawn();

        /// <summary>是否存在指定名称的可取出对象。</summary>
        /// <param name="name">对象名称；为 null 时匹配所有名称。</param>
        /// <returns>池中有符合名称且可用的对象则返回 true，否则返回 false。</returns>
        bool CanSpawn(string name);

        /// <summary>取出对象；没有可用时返回 null。</summary>
        /// <returns>可用对象实例，无可用时返回 null。</returns>
        T Spawn();

        /// <summary>取出指定名称对象；没有可用时返回 null。</summary>
        /// <param name="name">对象名称；为 null 时匹配所有名称。</param>
        /// <returns>符合名称的可用对象实例，无可用时返回 null。</returns>
        T Spawn(string name);

        /// <summary>
        /// 优先 Spawn；池中无可用实例时调用 factory 创建并 Register。
        /// 若已达 Capacity，会先尝试 Release 闲置对象腾出空位。
        /// </summary>
        /// <param name="factory">创建新对象的工厂方法，不可为 null，且不可返回 null。</param>
        /// <returns>取出或新建的对象实例。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException"><paramref name="factory"/> 返回 null。</exception>
        T SpawnOrCreate(Func<T> factory);

        /// <summary>带名称的 <see cref="SpawnOrCreate(Func{T})"/>。</summary>
        /// <param name="name">对象名称；为 null 时匹配所有名称。</param>
        /// <param name="factory">创建新对象的工厂方法，不可为 null，且不可返回 null。</param>
        /// <returns>取出或新建的对象实例。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException"><paramref name="factory"/> 返回 null。</exception>
        T SpawnOrCreate(string name, Func<T> factory);

        /// <summary>
        /// 使用 <see cref="SetFactory"/> 注册的工厂执行 SpawnOrCreate。
        /// 适合创建逻辑已封装在子类、并在建池时 Bind 一次的场景。
        /// </summary>
        /// <returns>取出或新建的对象实例。</returns>
        /// <exception cref="InvalidOperationException">尚未调用 <see cref="SetFactory"/>。</exception>
        T SpawnOrCreate();

        /// <summary>
        /// 注册默认工厂，供无参 <see cref="SpawnOrCreate()"/> 使用。
        /// 推荐在子类静态方法里绑定（如 <c>BulletObject.Bind(pool, prefab)</c>）。
        /// </summary>
        /// <param name="factory">创建新对象的工厂；传 null 可清除。</param>
        void SetFactory(Func<T> factory);

        /// <summary>归还对象。</summary>
        /// <param name="obj">要归还的对象，不可为 null，且必须属于此池。</param>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">对象不属于此池，或其 SpawnCount 已为 0。</exception>
        void Unspawn(T obj);

        /// <summary>按容量释放多余未使用对象。</summary>
        void Release();

        /// <summary>释放指定数量未使用对象。</summary>
        /// <param name="toReleaseCount">要释放的对象数量；小于 0 时无操作。</param>
        void Release(int toReleaseCount);

        /// <summary>释放全部未使用对象。</summary>
        void ReleaseAllUnused();
    }
}
