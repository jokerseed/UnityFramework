using System;

namespace Framework.ECS
{
    /// <summary>World 组件查询扩展：以驱动组件集合为迭代入口，只访问同时拥有所需组件的实体。</summary>
    public static class EntityQuery
    {
        /// <summary>遍历同时拥有 <typeparamref name="TDriver"/> 与 <typeparamref name="TRequired"/> 的实体。</summary>
        /// <typeparam name="TDriver">驱动组件类型（迭代入口）。</typeparam>
        /// <typeparam name="TRequired">必须同时存在的组件类型。</typeparam>
        /// <param name="world">ECS 世界。</param>
        /// <param name="action">回调：(entityId, driver, required)。</param>
        public static void ForEach<TDriver, TRequired>(
            this World world,
            Action<uint, TDriver, TRequired> action)
            where TDriver : struct, IComponent
            where TRequired : struct, IComponent
        {
            var driverStorage = world.GetStorage<TDriver>();
            var requiredStorage = world.GetStorage<TRequired>();

            foreach (var pair in driverStorage.All)
            {
                if (requiredStorage.TryGet(pair.Key, out var required))
                {
                    action(pair.Key, pair.Value, required);
                }
            }
        }

        /// <summary>遍历同时拥有 <typeparamref name="TDriver"/> 与两个附加组件的实体。</summary>
        /// <typeparam name="TDriver">驱动组件类型（迭代入口）。</typeparam>
        /// <typeparam name="T1">附加组件 1。</typeparam>
        /// <typeparam name="T2">附加组件 2。</typeparam>
        /// <param name="world">ECS 世界。</param>
        /// <param name="action">回调：(entityId, driver, t1, t2)。</param>
        public static void ForEach<TDriver, T1, T2>(
            this World world,
            Action<uint, TDriver, T1, T2> action)
            where TDriver : struct, IComponent
            where T1 : struct, IComponent
            where T2 : struct, IComponent
        {
            var driverStorage = world.GetStorage<TDriver>();
            var storage1 = world.GetStorage<T1>();
            var storage2 = world.GetStorage<T2>();

            foreach (var pair in driverStorage.All)
            {
                if (storage1.TryGet(pair.Key, out var c1) &&
                    storage2.TryGet(pair.Key, out var c2))
                {
                    action(pair.Key, pair.Value, c1, c2);
                }
            }
        }
    }
}
