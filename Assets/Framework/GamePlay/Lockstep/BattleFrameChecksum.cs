using System.Collections.Generic;
using Framework.Core;
using Framework.ECS.Components;
using Framework.FixedMath;

namespace Framework.GamePlay
{
    /// <summary>
    /// 逻辑帧结束后的轻量世界校验和：Actor 定点位姿、生命、随机状态、投射物。
    /// 不依赖 <c>Framework.Lockstep.ChecksumExtractor</c>（那套需要物理管理器）。
    /// </summary>
    public static class BattleFrameChecksum
    {
        static readonly List<ActorId> s_actorIds = new List<ActorId>(64);
        static readonly List<uint> s_projectileIds = new List<uint>(32);

        /// <summary>计算当前模拟状态的 64 位 FNV 哈希。</summary>
        /// <param name="framework">玩法框架；不可为 null。</param>
        /// <returns>状态哈希；framework 为 null 时返回 0。</returns>
        public static long Compute(GamePlayFramework framework)
        {
            if (framework == null)
            {
                return 0;
            }

            var hash = Fnv64.Create();
            hash.Add(framework.Random.GetStateHash());

            s_actorIds.Clear();
            foreach (var pair in framework.Registry.Actors)
            {
                s_actorIds.Add(pair.Key);
            }

            s_actorIds.Sort((a, b) => a.Value.CompareTo(b.Value));
            hash.Add(s_actorIds.Count);

            var world = framework.EcsWorld;
            var transforms = world.GetStorage<TransformComponent>();
            var velocities = world.GetStorage<VelocityComponent>();
            var combat = world.GetStorage<CombatStateComponent>();

            for (var i = 0; i < s_actorIds.Count; i++)
            {
                var id = s_actorIds[i];
                hash.Add((int)id.Value);
                if (!framework.Registry.TryGet(id, out var actor))
                {
                    continue;
                }

                hash.Add(actor.TeamId);
                hash.Add(actor.AbilitySystem.IsDead ? 1 : 0);
                hash.Add(actor.AbilitySystem.Attributes.GetCurrentValue(BattleConstants.Health).RawValue);
                if (actor.EcsEntity == null)
                {
                    continue;
                }

                var entityId = actor.EcsEntity.Id;
                if (transforms.TryGet(entityId, out var transform))
                {
                    hash.Add(transform.Position.x.RawValue);
                    hash.Add(transform.Position.y.RawValue);
                    hash.Add(transform.Position.z.RawValue);
                    hash.Add(transform.Forward.x.RawValue);
                    hash.Add(transform.Forward.z.RawValue);
                }

                if (velocities.TryGet(entityId, out var velocity))
                {
                    hash.Add(velocity.Value.x.RawValue);
                    hash.Add(velocity.Value.z.RawValue);
                }

                if (combat.TryGet(entityId, out var state))
                {
                    hash.Add(state.IsAlive ? 1 : 0);
                }
            }

            var projectiles = world.GetStorage<ProjectileComponent>();
            s_projectileIds.Clear();
            foreach (var pair in projectiles.All)
            {
                s_projectileIds.Add(pair.Key);
            }

            s_projectileIds.Sort();
            hash.Add(s_projectileIds.Count);
            for (var i = 0; i < s_projectileIds.Count; i++)
            {
                var entityId = s_projectileIds[i];
                hash.Add((int)entityId);
                if (!projectiles.TryGet(entityId, out var projectile))
                {
                    continue;
                }

                hash.Add((int)projectile.Owner.Value);
                hash.Add(projectile.Damage.RawValue);
                hash.Add(projectile.RemainingLifetime.RawValue);
                if (transforms.TryGet(entityId, out var transform))
                {
                    hash.Add(transform.Position.x.RawValue);
                    hash.Add(transform.Position.z.RawValue);
                }
            }

            return hash.Value;
        }

        struct Fnv64
        {
            ulong _hash;

            public long Value => unchecked((long)_hash);

            public static Fnv64 Create()
            {
                Fnv64 result;
                result._hash = 14695981039346656037UL;
                return result;
            }

            public void Add(int value) => Add((ulong)(uint)value);

            public void Add(long value) => Add((ulong)value);

            public void Add(ulong value)
            {
                unchecked
                {
                    _hash ^= value;
                    _hash *= 1099511628211UL;
                }
            }
        }
    }
}
