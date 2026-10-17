using System;
using System.Collections.Generic;
using Framework.Battle;
using Framework.ECS.Components;
using Framework.FixedMath;

namespace Framework.GamePlay
{
    /// <summary>
    /// 逻辑帧结束后的轻量世界校验和。
    /// 覆盖：随机状态、按 ActorId 排序的生命/法力/护盾/死亡、标签计数、活跃效果（含冷却）剩余时间与层数、
    /// 激活中的技能 ID、实例 ID、状态与任务进度、已绑定 Agent 的私有随机数、权威黑板和行为树槽位、
    /// 定点位姿与速度、击退、存活标记、投射物（队伍、穿透、爆炸半径、伤害类型、寿命与位置）。
    /// 不覆盖：Farseer 接触流形与睡眠岛、表现层 float 位姿、黑板 object 袋。
    /// 没有激活技能实例、也没有 Agent 时，哈希输入与加入技能任务和 Agent 字段之前相同。
    /// 激活实例没有任务时，仍只写技能 ID 和状态，不写实例 ID。有任务才追加实例 ID、任务数和进度。
    /// 不依赖 <c>Framework.Lockstep.ChecksumExtractor</c>（那套需要物理管理器）。
    /// </summary>
    public static class BattleFrameChecksum
    {
        // 排序用的临时缓冲按线程各一份：服务端在多个线程上并行跑多场战斗时，共享的静态缓冲会被互相写乱。
        [ThreadStatic] static List<ActorId> s_actorIds;
        [ThreadStatic] static List<uint> s_projectileIds;
        [ThreadStatic] static List<KeyValuePair<string, int>> s_tags;
        [ThreadStatic] static List<KeyValuePair<string, int>> s_ints;
        [ThreadStatic] static List<KeyValuePair<string, bool>> s_bools;
        [ThreadStatic] static List<KeyValuePair<string, long>> s_fps;
        [ThreadStatic] static List<KeyValuePair<string, uint>> s_ids;
        [ThreadStatic] static List<KeyValuePair<string, string>> s_strings;
        static readonly Comparison<KeyValuePair<string, int>> s_tagOrder =
            (left, right) => string.CompareOrdinal(left.Key, right.Key);
        static readonly Comparison<KeyValuePair<string, bool>> s_boolOrder =
            (left, right) => string.CompareOrdinal(left.Key, right.Key);
        static readonly Comparison<KeyValuePair<string, long>> s_fpOrder =
            (left, right) => string.CompareOrdinal(left.Key, right.Key);
        static readonly Comparison<KeyValuePair<string, uint>> s_idOrder =
            (left, right) => string.CompareOrdinal(left.Key, right.Key);
        static readonly Comparison<KeyValuePair<string, string>> s_stringOrder =
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

            EnsureBuffers();
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
                if (framework.TryGetBattleAgent(id, out var agent))
                {
                    HashAgent(ref hash, agent);
                }
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

        static void EnsureBuffers()
        {
            if (s_actorIds != null)
            {
                return;
            }

            s_actorIds = new List<ActorId>(64);
            s_projectileIds = new List<uint>(32);
            s_tags = new List<KeyValuePair<string, int>>(8);
            s_ints = new List<KeyValuePair<string, int>>(8);
            s_bools = new List<KeyValuePair<string, bool>>(8);
            s_fps = new List<KeyValuePair<string, long>>(8);
            s_ids = new List<KeyValuePair<string, uint>>(8);
            s_strings = new List<KeyValuePair<string, string>>(8);
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
                var tasks = instance.Tasks;
                if (tasks.Count == 0)
                {
                    continue;
                }

                hash.Add(instance.InstanceId);
                hash.Add(tasks.Count);
                for (var t = 0; t < tasks.Count; t++)
                {
                    var task = tasks[t];
                    hash.Add(task.GetType().Name);
                    hash.Add(task.ProgressHash());
                }
            }
        }

        static void HashAgent(ref Fnv64 hash, BattleAgent agent)
        {
            hash.Add(1);
            var random = agent.Random;
            hash.Add(random != null ? random.GetStateHash() : 0);
            hash.Add((int)agent.Tree.LastStatus);
            hash.Add(agent.Tree.Runtime.ProgressHash());
            var board = agent.Blackboard;
            board.CopyInts(s_ints);
            s_ints.Sort(s_tagOrder);
            hash.Add(s_ints.Count);
            for (var i = 0; i < s_ints.Count; i++)
            {
                hash.Add(s_ints[i].Key);
                hash.Add(s_ints[i].Value);
            }

            board.CopyBools(s_bools);
            s_bools.Sort(s_boolOrder);
            hash.Add(s_bools.Count);
            for (var i = 0; i < s_bools.Count; i++)
            {
                hash.Add(s_bools[i].Key);
                hash.Add(s_bools[i].Value ? 1 : 0);
            }

            board.CopyFpRaws(s_fps);
            s_fps.Sort(s_fpOrder);
            hash.Add(s_fps.Count);
            for (var i = 0; i < s_fps.Count; i++)
            {
                hash.Add(s_fps[i].Key);
                hash.Add(s_fps[i].Value);
            }

            board.CopyIds(s_ids);
            s_ids.Sort(s_idOrder);
            hash.Add(s_ids.Count);
            for (var i = 0; i < s_ids.Count; i++)
            {
                hash.Add(s_ids[i].Key);
                hash.Add((int)s_ids[i].Value);
            }

            board.CopyStrings(s_strings);
            s_strings.Sort(s_stringOrder);
            hash.Add(s_strings.Count);
            for (var i = 0; i < s_strings.Count; i++)
            {
                hash.Add(s_strings[i].Key);
                hash.Add(s_strings[i].Value);
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
