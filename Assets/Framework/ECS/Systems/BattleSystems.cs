using System.Collections.Generic;
using Framework.Core;
using Framework.ECS.Components;
using Framework.FixedMath;

namespace Framework.ECS.Systems
{
    /// <summary>
    /// 空间索引系统，在物理步进之后重建 Actor 空间索引，
    /// 供 GAS 目标查询使用。
    /// </summary>
    public sealed class SpatialIndexSystem : ISystem
    {
        SpatialHashGrid _grid;

        /// <inheritdoc/>
        public EcsSystemPhase Phase => EcsSystemPhase.Simulate;

        /// <summary>系统创建时初始化 <see cref="SpatialHashGrid"/> 并注册为 World 单例。</summary>
        /// <param name="world">拥有该系统的 ECS 世界。</param>
        public void OnCreate(World world)
        {
            _grid = new SpatialHashGrid(BattleConstants.SpatialCellSize);
            world.RegisterSingleton(_grid);
        }

        /// <inheritdoc/>
        public void OnDestroy(World world) { }

        /// <summary>物理步进之后重建 Actor 空间索引。</summary>
        /// <param name="world">拥有该系统的 ECS 世界。</param>
        /// <param name="deltaTime">距上一帧的时间间隔（秒，定点）；本系统未使用。</param>
        public void Update(World world, FP deltaTime)
        {
            SpatialIndexService.RebuildActors(world, _grid);
        }
    }

    /// <summary>
    /// 投射物生命周期系统，每帧递减 <see cref="ProjectileComponent.RemainingLifetime"/>，
    /// 到期时销毁对应实体（同时卸下 Farseer 刚体）。
    /// </summary>
    public sealed class ProjectileLifetimeSystem : ISystem
    {
        readonly List<uint> _ids = new List<uint>(32);
        readonly List<uint> _toDestroy = new List<uint>(16);

        /// <inheritdoc/>
        public EcsSystemPhase Phase => EcsSystemPhase.Simulate;

        /// <inheritdoc/>
        public void OnCreate(World world) { }

        /// <inheritdoc/>
        public void OnDestroy(World world) { }

        /// <summary>遍历所有投射物，递减剩余生命时间并销毁已到期的实体。</summary>
        /// <param name="world">拥有该系统的 ECS 世界。</param>
        /// <param name="deltaTime">距上一帧的时间间隔（秒，定点）。</param>
        public void Update(World world, FP deltaTime)
        {
            _toDestroy.Clear();
            _ids.Clear();
            var projectiles = world.GetStorage<ProjectileComponent>();

            foreach (var pair in projectiles.All)
            {
                _ids.Add(pair.Key);
            }

            for (var i = 0; i < _ids.Count; i++)
            {
                var entityId = _ids[i];
                if (!projectiles.TryGet(entityId, out var projectile))
                {
                    continue;
                }

                projectile.RemainingLifetime -= deltaTime;
                projectiles.Add(entityId, projectile);

                if (projectile.RemainingLifetime <= FP.Zero)
                {
                    _toDestroy.Add(entityId);
                }
            }

            for (var i = 0; i < _toDestroy.Count; i++)
            {
                world.DestroyEntity(_toDestroy[i]);
            }
        }
    }
}
