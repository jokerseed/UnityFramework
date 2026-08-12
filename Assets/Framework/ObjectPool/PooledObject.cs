using System;

namespace Framework.ObjectPool
{
    /// <summary>
    /// 自持对象池的池化对象基类：池创建、工厂、Spawn/Unspawn 都由类型自身管理。
    /// 业务侧直接 <c>T.Spawn()</c> / <c>T.Unspawn(obj)</c>；prefab/容量等在子类内部配置，无参初始化。
    /// </summary>
    /// <typeparam name="TSelf">具体子类自身类型（CRTP）。</typeparam>
    public abstract class PooledObject<TSelf> : ObjectBase where TSelf : PooledObject<TSelf>, new()
    {
        static IObjectPool<TSelf> _pool;
        static Func<TSelf> _factory;
        static Action _autoSetup;

        /// <summary>本类型默认池名称（默认用类型名）。</summary>
        protected static string DefaultPoolName => typeof(TSelf).Name;

        /// <summary>是否已完成池初始化。</summary>
        public static bool IsReady => _pool != null && _factory != null;

        /// <summary>当前绑定的对象池；未初始化时为 null。</summary>
        public static IObjectPool<TSelf> Pool => _pool;

        /// <summary>
        /// 注册无参自动初始化逻辑（通常在子类静态构造函数中调用）。
        /// 首次 <see cref="Spawn"/> / <see cref="Unspawn"/> / <see cref="Setup"/> 时执行。
        /// </summary>
        /// <param name="autoSetup">无参初始化回调，内部应调用 <see cref="SetupPool"/>。</param>
        protected static void UseAutoSetup(Action autoSetup)
        {
            _autoSetup = autoSetup ?? throw new ArgumentNullException(nameof(autoSetup));
        }

        /// <summary>
        /// 显式无参初始化（可选）。未调用时，首次 <see cref="Spawn"/> 也会自动执行 <see cref="UseAutoSetup"/> 注册的逻辑。
        /// </summary>
        public static void Setup()
        {
            EnsureReady();
        }

        /// <summary>
        /// 创建或复用本类型对象池，并注册工厂。
        /// 由子类自动初始化回调调用；prefab/路径/容量等封闭在子类内，不要从外部传入。
        /// </summary>
        /// <param name="factory">新建实例的工厂，不可为 null。</param>
        /// <param name="allowMultiSpawn">是否允许多次 Spawn（引用计数）。</param>
        /// <param name="capacity">容量上限。</param>
        /// <param name="expireTime">闲置过期时间（秒）。</param>
        /// <param name="autoReleaseInterval">自动释放检查间隔（秒）。</param>
        /// <param name="poolName">池名称；默认类型名。</param>
        /// <param name="priority">池优先级。</param>
        /// <exception cref="ArgumentNullException"><paramref name="factory"/> 为 null。</exception>
        protected static void SetupPool(
            Func<TSelf> factory,
            bool allowMultiSpawn = true,
            int capacity = int.MaxValue,
            float expireTime = float.MaxValue,
            float autoReleaseInterval = 60f,
            string poolName = null,
            int priority = 0)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            _factory = factory;
            var name = string.IsNullOrEmpty(poolName) ? DefaultPoolName : poolName;
            var manager = ObjectPoolManager.Instance;

            if (manager.HasObjectPool<TSelf>(name))
            {
                _pool = manager.GetObjectPool<TSelf>(name);
            }
            else
            {
                _pool = allowMultiSpawn
                    ? manager.CreateMultiSpawnObjectPool<TSelf>(
                        name, autoReleaseInterval, capacity, expireTime, priority)
                    : manager.CreateSingleSpawnObjectPool<TSelf>(
                        name, autoReleaseInterval, capacity, expireTime, priority);
            }

            _pool.SetFactory(_factory);
        }

        /// <summary>从本类型池取出或新建实例。</summary>
        /// <returns>可用实例。</returns>
        /// <exception cref="InvalidOperationException">子类未通过 <see cref="UseAutoSetup"/> 配置初始化。</exception>
        public static TSelf Spawn()
        {
            EnsureReady();
            return _pool.SpawnOrCreate();
        }

        /// <summary>归还实例到本类型池。</summary>
        /// <param name="obj">要归还的实例，不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="obj"/> 为 null。</exception>
        /// <exception cref="InvalidOperationException">未初始化，或对象不属于本池。</exception>
        public static void Unspawn(TSelf obj)
        {
            if (obj == null)
            {
                throw new ArgumentNullException(nameof(obj));
            }

            EnsureReady();
            _pool.Unspawn(obj);
        }

        /// <summary>
        /// 销毁本类型池并清空静态绑定。
        /// 通常在关卡卸载或模块 Shutdown 时调用。
        /// </summary>
        public static void TearDown()
        {
            if (_pool == null)
            {
                _factory = null;
                return;
            }

            var name = _pool.Name;
            ObjectPoolManager.Instance.DestroyObjectPool<TSelf>(name);
            _pool = null;
            _factory = null;
        }

        static void EnsureReady()
        {
            if (IsReady)
            {
                return;
            }

            _autoSetup?.Invoke();

            if (!IsReady)
            {
                throw new InvalidOperationException(
                    $"{typeof(TSelf).Name} is not configured. " +
                    $"In static constructor call UseAutoSetup(() => SetupPool(...)) with prefab/capacity inside the type.");
            }
        }
    }
}
