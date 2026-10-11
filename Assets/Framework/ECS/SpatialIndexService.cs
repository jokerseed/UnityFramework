using Framework.ECS.Components;

namespace Framework.ECS
{
    /// <summary>Actor 空间索引重建服务，供 GAS 目标查询共享。</summary>
    public static class SpatialIndexService
    {
        /// <summary>清空并重建网格，索引所有存活且拥有 <see cref="ActorLinkComponent"/> 的实体位置。</summary>
        /// <param name="world">ECS 世界。</param>
        /// <param name="grid">目标空间哈希网格。</param>
        public static void RebuildActors(World world, SpatialHashGrid grid)
        {
            grid.Clear();
            var combat = world.GetStorage<CombatStateComponent>();
            var transforms = world.GetStorage<TransformComponent>();
            foreach (var pair in world.GetStorage<ActorLinkComponent>())
            {
                var entityId = pair.Key;
                if (combat.TryGet(entityId, out var state) && !state.IsAlive)
                {
                    continue;
                }

                if (!transforms.TryGet(entityId, out var transform))
                {
                    continue;
                }

                grid.Insert(entityId, transform.Position);
            }

            grid.ClearStructuralDirty();
        }
    }
}
