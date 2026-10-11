using System.Collections.Generic;
using Framework.Core;
using Framework.ECS.Components;
using Framework.FixedMath;

namespace Framework.GamePlay
{
    /// <summary>
    /// 逻辑帧结束后的轻量世界校验和。
    /// 覆盖：随机状态、按 ActorId 排序的生命/法力/护盾/死亡、标签计数、活跃效果（含冷却）剩余时间与层数、
    /// 激活中的技能 ID 与状态、定点位姿与速度、击退、存活标记、投射物（队伍、穿透、爆炸半径、伤害类型、寿命与位置）。
    /// 不覆盖：Farseer 接触流形与睡眠岛、行为树内部节点游标、表现层 float 位姿。
    /// 不依赖 <c>Framework.Lockstep.ChecksumExtractor</c>（那套需要物理管理器）。
    /// </summary>
    public static class BattleFrameChecksum
    {
        static readonly List<ActorId> s_actorIds = new List<ActorId>(64);
        static readonly List<uint> s_projectileIds = new List<uint>(32);
        static readonly List<KeyValuePair<string, int>> s_tags = new List<KeyValuePair<string, int>>(8);
        static readonly Comparison<KeyValuePair<string, int>> s_tagOrder =
            (left, right) => string.CompareOrdinal(left.Key, right.Key);

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

            framework.Registry.CopySortedActorIds(s_actorIds);
            hash.Add(s_actorIds.Count);

            var world = framework.EcsWorld;
            var transforms = world.GetStorage<TransformComponent>();
            var velocities = world.GetStorage<VelocityComponent>();
            var knockbacks = world.GetStorage<KnockbackComponent>();
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
                var asc = actor.AbilitySystem;
                hash.Add(asc.IsDead ? 1 : 0);
                hash.Add(asc.Attributes.GetCurrentValue(BattleConstants.Health).RawValue);
                hash.Add(asc.Attributes.GetCurrentValue(BattleConstants.Mana).RawValue);
                hash.Add(asc.Attributes.GetCurrentValue(BattleConstants.Shield).RawValue);
                HashTags(ref hash, asc);
                HashEffects(ref hash, asc);
                HashAbilities(ref hash, asc);
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
                    hash.Add(velocity.Value.y.RawValue);
                    hash.Add(velocity.Value.z.RawValue);
                }

                if (knockbacks.TryGet(entityId, out var knockback))
                {
                    hash.Add(1);
                    hash.Add(knockback.Velocity.x.RawValue);
                    hash.Add(knockback.Velocity.y.RawValue);
                    hash.Add(knockback.Velocity.z.RawValue);
                    hash.Add(knockback.Remaining.RawValue);
                }
                else
                {
                    hash.Add(0);
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
                hash.Add(projectile.TeamId);
                hash.Add(projectile.PierceRemaining);
                hash.Add((int)projectile.DamageType);
                hash.Add(projectile.Damage.RawValue);
                hash.Add(projectile.ExplodeRadius.RawValue);
                hash.Add(projectile.RemainingLifetime.RawValue);
                hash.Add(projectile.AbilityId);
                hash.Add(projectile.HitEffectId);
                if (transforms.TryGet(entityId, out var transform))
                {
                    hash.Add(transform.Position.x.RawValue);
                    hash.Add(transform.Position.y.RawValue);
                    hash.Add(transform.Position.z.RawValue);
                }
            }

            return hash.Value;
        }

        static void HashTags(ref Fnv64 hash, GAS.AbilitySystemComponent asc)
        {
            asc.Tags.CopyCounts(s_tags);
            s_tags.Sort(s_tagOrder);
            hash.Add(s_tags.Count);
            for (var i = 0; i < s_tags.Count; i++)
            {
                hash.Add(s_tags[i].Key);
                hash.Add(s_tags[i].Value);
            }
        }

        static void HashEffects(ref Fnv64 hash, GAS.AbilitySystemComponent asc)
        {
            var count = asc.ActiveEffectCount;
            hash.Add(count);
            for (var i = 0; i < count; i++)
            {
                var effect = asc.GetActiveEffect(i);
                hash.Add(effect.Spec.EffectId);
                hash.Add((int)effect.Source.Value);
                hash.Add(effect.StackCount);
                hash.Add(effect.RemainingTime.RawValue);
                hash.Add(effect.PeriodTimer.RawValue);
                hash.Add((int)effect.Spec.DurationPolicy);
            }
        }

        static void HashAbilities(ref Fnv64 hash, GAS.AbilitySystemComponent asc)
        {
            var instances = asc.ActiveInstances;
            hash.Add(instances.Count);
            for (var i = 0; i < instances.Count; i++)
            {
                var instance = instances[i];
                hash.Add(instance.Spec.Def.AbilityId);
                hash.Add((int)instance.State);
            }
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

            public void Add(string value)
            {
                if (string.IsNullOrEmpty(value))
                {
                    Add(0);
                    return;
                }

                Add(value.Length);
                for (var i = 0; i < value.Length; i++)
                {
                    Add((ulong)value[i]);
                }
            }
        }
    }
}
