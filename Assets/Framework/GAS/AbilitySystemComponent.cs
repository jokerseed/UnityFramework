using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Events;
using Framework.GAS.Abilities;
using Framework.GAS.Attributes;
using Framework.GAS.Combat;
using Framework.GAS.Cues;
using Framework.GAS.Effects;
using Framework.GAS.Events;
using Framework.GAS.Tags;
using UnityEngine;

namespace Framework.GAS
{
    /// <summary>
    /// 技能系统组件（ASC），是 GAS 的核心运行时对象。
    /// 每个战斗单位持有一个 ASC，负责管理技能 Spec、属性、标签、持续效果与冷却。
    /// </summary>
    public sealed class AbilitySystemComponent
    {
        int _nextSpecHandle = 1;

        readonly Dictionary<GameplayAbilitySpecHandle, GameplayAbilitySpec> _grantedSpecs =
            new Dictionary<GameplayAbilitySpecHandle, GameplayAbilitySpec>();
        readonly Dictionary<string, List<GameplayAbilitySpecHandle>> _specsByAbilityId =
            new Dictionary<string, List<GameplayAbilitySpecHandle>>();
        readonly List<ActiveAbilityInstance> _activeInstances = new List<ActiveAbilityInstance>();
        readonly List<ActiveGameplayEffect> _activeEffects = new List<ActiveGameplayEffect>();
        readonly List<ActiveAbilityInstance> _activeInstancesScratch = new List<ActiveAbilityInstance>();
        readonly AttributeModifierAggregator _aggregator = new AttributeModifierAggregator();
        float _appliedShieldBonus;

        /// <summary>所属单位的唯一标识符。</summary>
        public ActorId ActorId { get; }

        /// <summary>属性集合（生命值、攻击力等）。</summary>
        public GameplayAttributeSet Attributes { get; } = new GameplayAttributeSet();

        /// <summary>当前持有的 GameplayTag 集合（带引用计数）。</summary>
        public GameplayTagContainer Tags { get; } = new GameplayTagContainer();

        /// <summary>伤害处理管线；默认为 <see cref="DefaultDamageProcessor"/>，可替换为自定义实现。</summary>
        public IDamageProcessor DamageProcessor { get; set; } = new DefaultDamageProcessor();

        /// <summary>当前激活中的技能实例（只读）。</summary>
        public IReadOnlyList<ActiveAbilityInstance> ActiveInstances => _activeInstances;

        /// <summary>是否已死亡（持有死亡标签）。</summary>
        public bool IsDead => Tags.HasTag(new GameplayTag(BattleConstants.TagDead));

        /// <summary>Cue 管理器；为 null 时直接向 EventBus 发布 <see cref="GameplayCueEvent"/>。</summary>
        public IGameplayCueManager CueManager { get; set; }

        /// <summary>Cue 使用的世界坐标；由 GamePlay 每帧同步。</summary>
        public Vector3 CuePosition { get; set; }

        /// <summary>Cue 使用的朝向；由 GamePlay 每帧同步。</summary>
        public Vector3 CueDirection { get; set; } = Vector3.forward;

        /// <summary>创建与指定单位绑定的技能系统组件。</summary>
        /// <param name="actorId">所属单位标识符。</param>
        public AbilitySystemComponent(ActorId actorId)
        {
            ActorId = actorId;
        }

        /// <summary>当前是否有指定技能处于激活中。</summary>
        /// <param name="abilityId">技能 ID。</param>
        /// <returns>存在 Active 实例时返回 true。</returns>
        public bool HasActiveAbility(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId))
            {
                return false;
            }

            for (var i = 0; i < _activeInstances.Count; i++)
            {
                var instance = _activeInstances[i];
                if (instance.State == ActiveAbilityState.Active &&
                    instance.Spec.Def.AbilityId == abilityId)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>授予技能并返回 Spec 句柄；同 AbilityId 可并存多份 Spec。</summary>
        /// <param name="def">技能定义；不可为 null。</param>
        /// <param name="level">技能等级。</param>
        /// <param name="inputId">输入绑定 ID。</param>
        /// <returns>授予后的 Spec 句柄。</returns>
        public GameplayAbilitySpecHandle GiveAbility(GameplayAbilityDef def, int level = 1, int inputId = -1)
        {
            if (def == null)
            {
                throw new ArgumentNullException(nameof(def));
            }

            var handle = new GameplayAbilitySpecHandle(_nextSpecHandle++);
            var ability = def.CreateAbility();
            var spec = new GameplayAbilitySpec(handle, def, ability, level, inputId);
            _grantedSpecs[handle] = spec;
            if (!_specsByAbilityId.TryGetValue(def.AbilityId, out var list))
            {
                list = new List<GameplayAbilitySpecHandle>(2);
                _specsByAbilityId[def.AbilityId] = list;
            }

            list.Add(handle);
            return handle;
        }

        /// <summary>移除已授予技能，并取消该 Spec 正在释放的实例。</summary>
        /// <param name="handle">Spec 句柄。</param>
        /// <param name="eventBus">事件总线；为 null 时仍取消实例但不发表现事件。</param>
        /// <returns>移除成功返回 true。</returns>
        public bool RemoveAbility(GameplayAbilitySpecHandle handle, IEventBus eventBus = null)
        {
            if (!handle.IsValid || !_grantedSpecs.TryGetValue(handle, out var spec))
            {
                return false;
            }

            CancelInstancesForSpec(handle, eventBus);
            _grantedSpecs.Remove(handle);
            if (_specsByAbilityId.TryGetValue(spec.Def.AbilityId, out var list))
            {
                list.Remove(handle);
                if (list.Count == 0)
                {
                    _specsByAbilityId.Remove(spec.Def.AbilityId);
                }
            }

            return true;
        }

        /// <summary>注册技能（兼容 API）；内部调用 <see cref="GiveAbility"/>。</summary>
        /// <param name="ability">要注册的技能；不可为 null。</param>
        public void RegisterAbility(GameplayAbility ability) =>
            GiveAbility(new GameplayAbilityDef(ability));

        /// <summary>尝试按 Spec 句柄激活技能。</summary>
        /// <param name="handle">Spec 句柄。</param>
        /// <param name="context">激活上下文。</param>
        /// <param name="battle">战斗上下文。</param>
        /// <param name="instance">成功时输出激活实例。</param>
        /// <returns>激活结果。</returns>
        public AbilityActivationResult TryActivateAbility(
            GameplayAbilitySpecHandle handle,
            in AbilityActivationContext context,
            BattleContext battle,
            out ActiveAbilityInstance instance)
        {
            instance = null;
            if (!handle.IsValid || !_grantedSpecs.TryGetValue(handle, out var spec))
            {
                return AbilityActivationResult.Failed(AbilityActivationFailureReason.AbilityNotFound);
            }

            return TryActivateSpec(spec, context, battle, null, out instance);
        }

        /// <summary>尝试按技能 ID 激活该 ID 的第一份 Spec。</summary>
        /// <param name="abilityId">技能 ID。</param>
        /// <param name="context">激活上下文。</param>
        /// <param name="battle">战斗上下文。</param>
        /// <returns>激活结果。</returns>
        public AbilityActivationResult TryActivateAbility(
            string abilityId,
            in AbilityActivationContext context,
            BattleContext battle)
        {
            if (!TryGetSpecHandle(abilityId, out var handle) ||
                !_grantedSpecs.TryGetValue(handle, out var spec))
            {
                return AbilityActivationResult.Failed(AbilityActivationFailureReason.AbilityNotFound);
            }

            return TryActivateSpec(spec, context, battle, null, out _);
        }

        AbilityActivationResult TryActivateSpec(
            GameplayAbilitySpec spec,
            in AbilityActivationContext context,
            BattleContext battle,
            AbilityActivationInfo activationInfo,
            out ActiveAbilityInstance instance)
        {
            instance = null;
            var ability = spec.Ability;
            var canActivate = ability.CanActivate(this, context, spec);
            if (!canActivate.Success)
            {
                return canActivate;
            }

            CancelMatchingAbilities(spec.Def.CancelAbilitiesWithTags, battle.Presentation);

            activationInfo ??= new AbilityActivationInfo
            {
                Instigator = ActorId,
                TriggerTarget = context.PrimaryTarget
            };

            instance = new ActiveAbilityInstance(spec, context, activationInfo);
            _activeInstances.Add(instance);

            var cost = spec.Def.CostAttributes;
            if (cost != null && cost.Count > 0 && !TryPayCost(cost))
            {
                instance.State = ActiveAbilityState.Cancelled;
                _activeInstances.Remove(instance);
                instance = null;
                return AbilityActivationResult.Failed(AbilityActivationFailureReason.InsufficientResource);
            }

            if (!CommitAbility(instance, battle))
            {
                instance.State = ActiveAbilityState.Cancelled;
                _activeInstances.Remove(instance);
                instance = null;
                return AbilityActivationResult.Failed(AbilityActivationFailureReason.InsufficientResource);
            }

            instance.State = ActiveAbilityState.Active;
            GrantTags(spec.Def.ActivationOwnedTags, battle.Presentation);
            ability.Activate(this, instance, battle);

            if (instance.TaskRunner.Count == 0)
            {
                EndAbility(instance, battle.Presentation);
            }

            return AbilityActivationResult.Succeeded();
        }

        /// <summary>Commit 激活实例（消耗、冷却等）。</summary>
        /// <param name="instance">激活实例。</param>
        /// <param name="battle">战斗上下文。</param>
        /// <returns>Commit 成功返回 true。</returns>
        public bool CommitAbility(ActiveAbilityInstance instance, BattleContext battle)
        {
            if (instance == null || instance.State != ActiveAbilityState.Pending)
            {
                return false;
            }

            var ability = instance.Spec.Ability;
            if (ability.AutoCommit)
            {
                return ability.Commit(this, instance, battle);
            }

            return true;
        }

        /// <summary>正常结束激活实例。</summary>
        /// <param name="instance">激活实例。</param>
        /// <param name="eventBus">事件总线。</param>
        public void EndAbility(ActiveAbilityInstance instance, IEventBus eventBus)
        {
            if (instance == null || instance.State == ActiveAbilityState.Ended || instance.State == ActiveAbilityState.Cancelled)
            {
                return;
            }

            instance.TaskRunner.CancelAll();
            RemoveTags(instance.Spec.Def.ActivationOwnedTags, eventBus);
            instance.Spec.Ability.End(this, instance);
            instance.State = ActiveAbilityState.Ended;
            instance.ClearTasks();
            _activeInstances.Remove(instance);
        }

        /// <summary>取消激活实例。</summary>
        /// <param name="instanceId">实例 ID。</param>
        /// <param name="eventBus">事件总线。</param>
        /// <returns>找到并取消返回 true。</returns>
        public bool CancelAbility(int instanceId, IEventBus eventBus)
        {
            for (var i = 0; i < _activeInstances.Count; i++)
            {
                var instance = _activeInstances[i];
                if (instance.InstanceId != instanceId)
                {
                    continue;
                }

                CancelAbility(instance, eventBus);
                return true;
            }

            return false;
        }

        /// <summary>取消激活实例。</summary>
        /// <param name="instance">激活实例。</param>
        /// <param name="eventBus">事件总线。</param>
        public void CancelAbility(ActiveAbilityInstance instance, IEventBus eventBus)
        {
            if (instance == null || instance.State == ActiveAbilityState.Ended || instance.State == ActiveAbilityState.Cancelled)
            {
                return;
            }

            instance.TaskRunner.CancelAll();
            instance.State = ActiveAbilityState.Cancelled;
            RemoveTags(instance.Spec.Def.ActivationOwnedTags, eventBus);
            instance.Spec.Ability.End(this, instance);
            instance.ClearTasks();
            _activeInstances.Remove(instance);
        }

        /// <summary>取消全部正在释放的技能。</summary>
        /// <param name="eventBus">事件总线；可为 null。</param>
        public void CancelAllAbilities(IEventBus eventBus)
        {
            _activeInstancesScratch.Clear();
            _activeInstancesScratch.AddRange(_activeInstances);
            for (var i = 0; i < _activeInstancesScratch.Count; i++)
            {
                CancelAbility(_activeInstancesScratch[i], eventBus);
            }
        }

        /// <summary>取消 AssetTags / ActivationOwnedTags 匹配查询的激活技能。</summary>
        /// <param name="query">查询标签（支持层级前缀）。</param>
        /// <param name="eventBus">事件总线；可为 null。</param>
        /// <returns>取消的实例数量。</returns>
        public int CancelAbilitiesWithTag(GameplayTag query, IEventBus eventBus)
        {
            if (!query.IsValid)
            {
                return 0;
            }

            var cancelled = 0;
            _activeInstancesScratch.Clear();
            _activeInstancesScratch.AddRange(_activeInstances);
            for (var i = 0; i < _activeInstancesScratch.Count; i++)
            {
                var instance = _activeInstancesScratch[i];
                if (!AbilityDefMatchesTag(instance.Spec.Def, query))
                {
                    continue;
                }

                CancelAbility(instance, eventBus ?? NullEventBus.Instance);
                cancelled++;
            }

            return cancelled;
        }

        void CancelMatchingAbilities(IReadOnlyList<GameplayTag> queries, IEventBus eventBus)
        {
            if (queries == null || queries.Count == 0)
            {
                return;
            }

            for (var i = 0; i < queries.Count; i++)
            {
                CancelAbilitiesWithTag(queries[i], eventBus);
            }
        }

        static bool AbilityDefMatchesTag(GameplayAbilityDef def, GameplayTag query) =>
            TagsMatchQuery(def.AssetTags, query) || TagsMatchQuery(def.ActivationOwnedTags, query);

        /// <summary>以可驱散的冷却效果启动技能冷却。</summary>
        /// <param name="abilityId">冷却键（技能 ID 或共享组名）。</param>
        /// <param name="cooldown">冷却秒数。</param>
        /// <param name="eventBus">事件总线；可为 null（仍写入效果但不发 Cue）。</param>
        public void StartCooldown(string abilityId, float cooldown, IEventBus eventBus)
        {
            if (cooldown <= 0f || string.IsNullOrEmpty(abilityId))
            {
                return;
            }

            var def = new GameplayEffectDef(
                BattleConstants.CooldownEffectPrefix + abilityId,
                EffectDurationPolicy.Duration,
                cooldown,
                grantedTags: new[] { new GameplayTag(BattleConstants.TagCooldown) },
                stackingPolicy: EffectStackingPolicy.RefreshDuration);
            ApplyEffect(def, ActorId, eventBus ?? NullEventBus.Instance, sourceAsc: this);
        }

        /// <summary>将 GameplayEffect 应用到本 ASC。</summary>
        public bool ApplyEffect(GameplayEffectSpec spec, ActorId source, IEventBus eventBus) =>
            ApplyEffect(spec, source, eventBus, null, null);

        /// <summary>将 GameplayEffect 应用到本 ASC。</summary>
        /// <param name="spec">效果描述。</param>
        /// <param name="source">来源 Actor。</param>
        /// <param name="eventBus">事件总线。</param>
        /// <param name="setByCaller">SetByCaller 幅度；可为 null。</param>
        /// <returns>是否成功应用。</returns>
        public bool ApplyEffect(
            GameplayEffectSpec spec,
            ActorId source,
            IEventBus eventBus,
            IReadOnlyDictionary<string, float> setByCaller) =>
            ApplyEffect(spec, source, eventBus, setByCaller, null);

        /// <summary>将 GameplayEffect 应用到本 ASC。</summary>
        /// <param name="spec">效果描述。</param>
        /// <param name="source">来源 Actor。</param>
        /// <param name="eventBus">事件总线；不可为 null。</param>
        /// <param name="setByCaller">SetByCaller 幅度；可为 null。</param>
        /// <param name="sourceAsc">来源 ASC，供 Periodic Execution 使用；可为 null。</param>
        /// <returns>是否成功应用。</returns>
        public bool ApplyEffect(
            GameplayEffectSpec spec,
            ActorId source,
            IEventBus eventBus,
            IReadOnlyDictionary<string, float> setByCaller,
            AbilitySystemComponent sourceAsc)
        {
            if (spec == null || eventBus == null || IsDead)
            {
                return false;
            }

            if (!CanApplyEffect(spec))
            {
                return false;
            }

            if (spec.CostAttributes != null && spec.CostAttributes.Count > 0 && !TryPayCost(spec.CostAttributes))
            {
                return false;
            }

            if (spec.DurationPolicy == EffectDurationPolicy.Instant)
            {
                ApplyInstantEffect(spec, eventBus, setByCaller, sourceAsc);
                return true;
            }

            if (!TryStackOrAddEffect(spec, source, eventBus, sourceAsc))
            {
                return false;
            }

            RecalculateAttributes(eventBus);
            InterruptIfCrowdControlled(eventBus);
            return true;
        }

        /// <summary>应用 GameplayEffectDef。</summary>
        /// <param name="def">效果定义。</param>
        /// <param name="source">来源 Actor。</param>
        /// <param name="eventBus">事件总线。</param>
        /// <param name="setByCaller">SetByCaller；可为 null。</param>
        /// <param name="sourceAsc">来源 ASC；可为 null。</param>
        /// <returns>是否成功应用。</returns>
        public bool ApplyEffect(
            GameplayEffectDef def,
            ActorId source,
            IEventBus eventBus,
            IReadOnlyDictionary<string, float> setByCaller = null,
            AbilitySystemComponent sourceAsc = null) =>
            ApplyEffect(def.ToRuntimeSpec(setByCaller), source, eventBus, setByCaller, sourceAsc);

        /// <summary>治疗：增加 Health，不超过 MaxHealth。</summary>
        /// <param name="amount">治疗量。</param>
        /// <param name="eventBus">事件总线。</param>
        /// <returns>实际增加大于 0 时返回 true。</returns>
        public bool ApplyHeal(float amount, IEventBus eventBus)
        {
            if (amount <= 0f || IsDead || eventBus == null)
            {
                return false;
            }

            var health = Attributes.GetOrCreate(BattleConstants.Health);
            var old = health.CurrentValue;
            var max = Attributes.GetCurrentValue(BattleConstants.MaxHealth);
            var next = Math.Min(max <= 0f ? old + amount : max, old + amount);
            if (next <= old)
            {
                return false;
            }

            health.SetCurrentValue(next);
            eventBus.Publish(new AttributeChangedEvent
            {
                Actor = ActorId,
                AttributeName = BattleConstants.Health,
                OldValue = old,
                NewValue = next
            });
            return true;
        }

        /// <summary>按 GrantedTags 匹配驱散活跃效果。</summary>
        /// <param name="query">查询标签（支持层级前缀）。</param>
        /// <param name="maxCount">最多移除条数；≤0 表示全部匹配项。</param>
        /// <param name="eventBus">事件总线。</param>
        /// <returns>实际移除数量。</returns>
        public int DispelEffects(GameplayTag query, int maxCount, IEventBus eventBus)
        {
            if (!query.IsValid || eventBus == null)
            {
                return 0;
            }

            var removed = 0;
            for (var i = _activeEffects.Count - 1; i >= 0; i--)
            {
                if (maxCount > 0 && removed >= maxCount)
                {
                    break;
                }

                var effect = _activeEffects[i];
                if (!TagsMatchQuery(effect.Spec.GrantedTags, query))
                {
                    continue;
                }

                RemoveActiveEffectInternal(effect, eventBus);
                _activeEffects.RemoveAt(i);
                removed++;
            }

            if (removed > 0)
            {
                RecalculateAttributes(eventBus);
            }

            return removed;
        }

        /// <summary>将伤害上下文经管线处理后扣除生命值。</summary>
        public bool ApplyDamage(in DamageContext context, IEventBus eventBus, AbilitySystemComponent sourceAsc = null)
        {
            if (eventBus == null)
            {
                return false;
            }

            var processed = DamageProcessor.Process(context, sourceAsc, this);
            if (processed.FinalDamage <= 0f && processed.ShieldAbsorbed <= 0f)
            {
                eventBus.Publish(new DamageBlockedEvent
                {
                    Source = processed.Source,
                    Target = processed.Target,
                    AbilityId = processed.AbilityId,
                    RawDamage = processed.RawDamage
                });
                return false;
            }

            if (processed.ShieldAbsorbed > 0f)
            {
                var shield = Attributes.GetOrCreate(BattleConstants.Shield);
                eventBus.Publish(new AttributeChangedEvent
                {
                    Actor = ActorId,
                    AttributeName = BattleConstants.Shield,
                    OldValue = shield.CurrentValue + processed.ShieldAbsorbed,
                    NewValue = shield.CurrentValue
                });
            }

            var health = Attributes.GetOrCreate(BattleConstants.Health);
            var oldValue = health.CurrentValue;
            var newValue = Math.Max(0f, oldValue - processed.FinalDamage);
            health.SetCurrentValue(newValue);

            eventBus.Publish(new AttributeChangedEvent
            {
                Actor = ActorId,
                AttributeName = BattleConstants.Health,
                OldValue = oldValue,
                NewValue = newValue
            });

            eventBus.Publish(new DamageDealtEvent
            {
                Source = processed.Source,
                Target = processed.Target,
                AbilityId = processed.AbilityId,
                RawDamage = processed.RawDamage,
                FinalDamage = processed.FinalDamage,
                IsCrit = processed.IsCrit,
                ShieldAbsorbed = processed.ShieldAbsorbed,
                DamageType = processed.DamageType
            });

            if (newValue <= 0f)
            {
                HandleDeath(eventBus, processed.Source, processed.AbilityId);
            }

            return processed.FinalDamage > 0f || processed.ShieldAbsorbed > 0f;
        }

        /// <summary>每帧驱动激活技能 Task 与效果 Tick；已死亡或无活跃内容时跳过。</summary>
        /// <param name="deltaTime">帧间隔（秒）。</param>
        /// <param name="eventBus">事件总线。</param>
        public void Tick(float deltaTime, IEventBus eventBus)
        {
            if (IsDead)
            {
                return;
            }

            if (_activeInstances.Count > 0)
            {
                TickActiveAbilities(deltaTime, eventBus);
            }

            if (_activeEffects.Count > 0)
            {
                TickEffects(deltaTime, eventBus);
            }
        }

        /// <summary>获取剩余冷却（取该冷却键对应 GE 的剩余时间）。</summary>
        /// <param name="abilityId">技能 ID 或共享冷却键。</param>
        /// <returns>剩余秒数；无冷却为 0。</returns>
        public float CooldownRemaining(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId))
            {
                return 0f;
            }

            var effectId = BattleConstants.CooldownEffectPrefix + abilityId;
            var remaining = 0f;
            for (var i = 0; i < _activeEffects.Count; i++)
            {
                var effect = _activeEffects[i];
                if (effect.Spec.EffectId != effectId)
                {
                    continue;
                }

                if (effect.RemainingTime > remaining)
                {
                    remaining = effect.RemainingTime;
                }
            }

            return remaining;
        }

        /// <summary>尝试获取该技能 ID 的第一份 Spec 句柄。</summary>
        /// <param name="abilityId">技能 ID。</param>
        /// <param name="handle">找到时输出句柄。</param>
        /// <returns>存在至少一份 Spec 时返回 true。</returns>
        public bool TryGetSpecHandle(string abilityId, out GameplayAbilitySpecHandle handle)
        {
            if (_specsByAbilityId.TryGetValue(abilityId, out var list) && list.Count > 0)
            {
                handle = list[0];
                return true;
            }

            handle = default;
            return false;
        }

        /// <summary>初始化生命属性。</summary>
        /// <param name="maxHealth">最大生命。</param>
        public void InitializeHealth(float maxHealth) =>
            InitializeCombatAttributes(maxHealth, BattleConstants.DefaultMaxMana);

        /// <summary>初始化生命、法力、护盾与暴击等战斗属性。</summary>
        /// <param name="maxHealth">最大生命。</param>
        /// <param name="maxMana">最大法力。</param>
        public void InitializeCombatAttributes(float maxHealth, float maxMana)
        {
            Attributes.GetOrCreate(BattleConstants.MaxHealth, maxHealth).SetBaseValue(maxHealth);
            Attributes.GetOrCreate(BattleConstants.Health, maxHealth).SetCurrentValue(maxHealth);
            Attributes.GetOrCreate(BattleConstants.MaxMana, maxMana).SetBaseValue(maxMana);
            Attributes.GetOrCreate(BattleConstants.Mana, maxMana).SetCurrentValue(maxMana);
            Attributes.GetOrCreate(BattleConstants.Shield, 0f).SetCurrentValue(0f);
            Attributes.GetOrCreate(BattleConstants.MagicDefense, 0f);
            Attributes.GetOrCreate(BattleConstants.CritChance, 0f);
            Attributes.GetOrCreate(BattleConstants.CritMultiplier, BattleConstants.DefaultCritMultiplier)
                .SetBaseValue(BattleConstants.DefaultCritMultiplier);
            Attributes.GetOrCreate(BattleConstants.IncomingDamageMultiplier, 1f).SetBaseValue(1f);
        }

        /// <summary>清空持续效果与控制状态并回满资源，供 Actor 池回收复用。</summary>
        /// <param name="eventBus">事件总线；不可为 null。</param>
        /// <param name="maxHealth">复活后的最大生命。</param>
        public void ResetForReuse(IEventBus eventBus, float maxHealth)
        {
            if (eventBus == null)
            {
                throw new ArgumentNullException(nameof(eventBus));
            }

            CancelAllAbilities(eventBus);
            for (var i = _activeEffects.Count - 1; i >= 0; i--)
            {
                RemoveActiveEffectInternal(_activeEffects[i], eventBus);
            }

            _activeEffects.Clear();
            Tags.Clear();
            InitializeCombatAttributes(maxHealth, BattleConstants.DefaultMaxMana);
            RecalculateAttributes(eventBus);
        }

        /// <summary>若血量已空且尚未标记死亡，则补一次死亡处理。</summary>
        /// <param name="eventBus">事件总线。</param>
        public void SyncDeathIfNeeded(IEventBus eventBus)
        {
            if (IsDead)
            {
                return;
            }

            if (Attributes.GetCurrentValue(BattleConstants.Health) <= 0f)
            {
                HandleDeath(eventBus, ActorId.Invalid, null);
            }
        }

        void TickActiveAbilities(float deltaTime, IEventBus eventBus)
        {
            if (_activeInstances.Count == 0)
            {
                return;
            }

            _activeInstancesScratch.Clear();
            _activeInstancesScratch.AddRange(_activeInstances);

            for (var i = 0; i < _activeInstancesScratch.Count; i++)
            {
                var instance = _activeInstancesScratch[i];
                if (instance.State != ActiveAbilityState.Active)
                {
                    continue;
                }

                instance.TaskRunner.Tick(deltaTime);

                if (instance.State != ActiveAbilityState.Active)
                {
                    continue;
                }

                if (instance.Tasks.Count == 0)
                {
                    continue;
                }

                var allDone = true;
                for (var t = 0; t < instance.Tasks.Count; t++)
                {
                    var task = instance.Tasks[t];
                    if (!task.IsDone && !task.IsCancelled)
                    {
                        allDone = false;
                        break;
                    }
                }

                if (allDone)
                {
                    EndAbility(instance, eventBus);
                }
            }
        }

        bool CanApplyEffect(GameplayEffectSpec spec)
        {
            if (Tags.HasAny(spec.ApplicationBlockedTags))
            {
                return false;
            }

            if (spec.ImmunityTags != null && Tags.HasAny(spec.ImmunityTags))
            {
                return false;
            }

            return Tags.HasAll(spec.ApplicationRequiredTags);
        }

        bool TryStackOrAddEffect(
            GameplayEffectSpec spec,
            ActorId source,
            IEventBus eventBus,
            AbilitySystemComponent sourceAsc)
        {
            for (var i = 0; i < _activeEffects.Count; i++)
            {
                var existing = _activeEffects[i];
                if (!IsSameStackGroup(existing, spec, source))
                {
                    continue;
                }

                switch (spec.StackingPolicy)
                {
                    case EffectStackingPolicy.None:
                        return false;
                    case EffectStackingPolicy.RefreshDuration:
                        existing.RemainingTime = spec.Duration;
                        existing.PeriodTimer = spec.Period;
                        if (sourceAsc != null)
                        {
                            existing.SourceAsc = sourceAsc;
                        }

                        return true;
                    case EffectStackingPolicy.StackCount:
                        if (spec.MaxStacks <= 0 || existing.StackCount < spec.MaxStacks)
                        {
                            existing.StackCount++;
                        }

                        existing.RemainingTime = spec.Duration;
                        existing.PeriodTimer = spec.Period;
                        return true;
                    case EffectStackingPolicy.AggregateBySource:
                        existing.RemainingTime = spec.Duration;
                        existing.PeriodTimer = spec.Period;
                        if (sourceAsc != null)
                        {
                            existing.SourceAsc = sourceAsc;
                        }

                        return true;
                }
            }

            var active = new ActiveGameplayEffect(spec, source) { SourceAsc = sourceAsc };
            _activeEffects.Add(active);
            GrantTags(spec.GrantedTags, eventBus);
            GrantAbilities(active);
            PublishCueTags(spec.CueTagsOnApply, eventBus, GameplayCueAction.Add);
            if (spec.Period > 0f)
            {
                ExecutePeriodic(active, eventBus);
            }

            return true;
        }

        static bool IsSameStackGroup(ActiveGameplayEffect existing, GameplayEffectSpec spec, ActorId source)
        {
            if (existing.Spec.EffectId != spec.EffectId)
            {
                return false;
            }

            if (spec.StackingPolicy == EffectStackingPolicy.AggregateBySource)
            {
                return existing.Source == source;
            }

            return true;
        }

        void ApplyInstantEffect(
            GameplayEffectSpec spec,
            IEventBus eventBus,
            IReadOnlyDictionary<string, float> setByCaller,
            AbilitySystemComponent sourceAsc)
        {
            GrantTags(spec.GrantedTags, eventBus);
            PublishCueTags(spec.CueTagsOnApply, eventBus, GameplayCueAction.Execute);

            for (var i = 0; i < spec.Modifiers.Count; i++)
            {
                ApplyModifierInstant(spec.Modifiers[i], eventBus, setByCaller, sourceAsc);
            }

            if (spec.Executions != null && spec.Executions.Count > 0)
            {
                var source = sourceAsc ?? this;
                var execContext = new ExecutionContext(source, this, spec, setByCaller, eventBus);
                for (var i = 0; i < spec.Executions.Count; i++)
                {
                    spec.Executions[i].Execute(execContext);
                }
            }

            RemoveTags(spec.GrantedTags, eventBus);
        }

        void ApplyModifierInstant(
            EffectModifier modifier,
            IEventBus eventBus,
            IReadOnlyDictionary<string, float> setByCaller,
            AbilitySystemComponent sourceAsc)
        {
            var magnitude = modifier.Magnitude.Evaluate(sourceAsc ?? this, this, setByCaller);
            var attribute = Attributes.GetOrCreate(modifier.AttributeName);
            var oldValue = attribute.CurrentValue;

            switch (modifier.Operation)
            {
                case EffectModifierOperation.Add:
                    attribute.SetCurrentValue(oldValue + magnitude);
                    break;
                case EffectModifierOperation.Multiply:
                    attribute.SetCurrentValue(oldValue * magnitude);
                    break;
                case EffectModifierOperation.Override:
                    attribute.SetCurrentValue(magnitude);
                    break;
            }

            eventBus.Publish(new AttributeChangedEvent
            {
                Actor = ActorId,
                AttributeName = modifier.AttributeName,
                OldValue = oldValue,
                NewValue = attribute.CurrentValue
            });

            if (modifier.AttributeName == BattleConstants.Health && attribute.CurrentValue <= 0f)
            {
                HandleDeath(eventBus, sourceAsc?.ActorId ?? ActorId.Invalid, null);
            }
        }

        void TickEffects(float deltaTime, IEventBus eventBus)
        {
            var changed = false;
            for (var i = _activeEffects.Count - 1; i >= 0; i--)
            {
                var effect = _activeEffects[i];
                TickPeriodicEffect(effect, deltaTime, eventBus);

                if (effect.Spec.DurationPolicy != EffectDurationPolicy.Duration)
                {
                    continue;
                }

                effect.RemainingTime -= deltaTime;
                if (!effect.IsExpired)
                {
                    continue;
                }

                RemoveActiveEffectInternal(effect, eventBus);
                _activeEffects.RemoveAt(i);
                changed = true;
            }

            if (changed)
            {
                RecalculateAttributes(eventBus);
            }
        }

        void TickPeriodicEffect(ActiveGameplayEffect effect, float deltaTime, IEventBus eventBus)
        {
            if (effect.Spec.Period <= 0f)
            {
                return;
            }

            effect.PeriodTimer -= deltaTime;
            if (effect.PeriodTimer > 0f)
            {
                return;
            }

            effect.PeriodTimer = effect.Spec.Period;
            ExecutePeriodic(effect, eventBus);
        }

        void ExecutePeriodic(ActiveGameplayEffect effect, IEventBus eventBus)
        {
            if (effect.Spec.Executions == null || effect.Spec.Executions.Count == 0)
            {
                return;
            }

            var source = effect.SourceAsc ?? this;
            var execContext = new ExecutionContext(source, this, effect.Spec, effect.Spec.SetByCaller, eventBus);
            var stacks = effect.StackCount > 0 ? effect.StackCount : 1;
            for (var s = 0; s < stacks; s++)
            {
                for (var i = 0; i < effect.Spec.Executions.Count; i++)
                {
                    effect.Spec.Executions[i].Execute(execContext);
                }
            }
        }

        /// <summary>移除活跃效果。</summary>
        public bool RemoveActiveEffect(GameplayEffectHandle handle, IEventBus eventBus)
        {
            for (var i = 0; i < _activeEffects.Count; i++)
            {
                if (_activeEffects[i].Handle != handle)
                {
                    continue;
                }

                var effect = _activeEffects[i];
                RemoveActiveEffectInternal(effect, eventBus);
                _activeEffects.RemoveAt(i);
                RecalculateAttributes(eventBus);
                return true;
            }

            return false;
        }

        /// <summary>按 EffectId 移除所有匹配活跃效果。</summary>
        public int RemoveActiveEffectsWithId(string effectId, IEventBus eventBus)
        {
            var removed = 0;
            for (var i = _activeEffects.Count - 1; i >= 0; i--)
            {
                if (_activeEffects[i].Spec.EffectId != effectId)
                {
                    continue;
                }

                RemoveActiveEffectInternal(_activeEffects[i], eventBus);
                _activeEffects.RemoveAt(i);
                removed++;
            }

            if (removed > 0)
            {
                RecalculateAttributes(eventBus);
            }

            return removed;
        }

        void RemoveActiveEffectInternal(ActiveGameplayEffect effect, IEventBus eventBus)
        {
            RemoveTags(effect.Spec.GrantedTags, eventBus);
            RevokeGrantedAbilities(effect.GrantedAbilityHandles, eventBus);
            PublishCueTags(effect.Spec.CueTagsOnRemove, eventBus, GameplayCueAction.Remove);
            eventBus.Publish(new GameplayEffectRemovedEvent
            {
                Actor = ActorId,
                EffectId = effect.Spec.EffectId,
                Handle = effect.Handle
            });
        }

        void GrantAbilities(ActiveGameplayEffect effect)
        {
            var defs = effect.Spec.GrantedAbilityDefs;
            if (defs == null || defs.Count == 0)
            {
                return;
            }

            for (var i = 0; i < defs.Count; i++)
            {
                effect.GrantedAbilityHandles.Add(GiveAbility(defs[i]));
            }
        }

        void PublishCueTags(IReadOnlyList<string> cueTags, IEventBus eventBus, GameplayCueAction action)
        {
            if (cueTags == null || cueTags.Count == 0)
            {
                return;
            }

            var direction = CueDirection.sqrMagnitude > 0f ? CueDirection.normalized : Vector3.forward;
            var parameters = new GameplayCueParameters(ActorId, CuePosition, direction);
            for (var i = 0; i < cueTags.Count; i++)
            {
                var cueTag = cueTags[i];
                if (CueManager != null)
                {
                    switch (action)
                    {
                        case GameplayCueAction.Add:
                            CueManager.AddCue(cueTag, parameters);
                            break;
                        case GameplayCueAction.Remove:
                            CueManager.RemoveCue(cueTag, parameters);
                            break;
                        default:
                            CueManager.ExecuteCue(cueTag, parameters);
                            break;
                    }
                }
                else
                {
                    eventBus.Publish(new GameplayCueEvent
                    {
                        Actor = ActorId,
                        CueTag = cueTag,
                        Position = CuePosition,
                        Direction = direction,
                        Action = action
                    });
                }
            }
        }

        /// <summary>处理 GameplayEvent，尝试触发匹配 TriggerTag 的被动技能。</summary>
        public bool HandleGameplayEvent(in GameplayEventData eventData, BattleContext battle)
        {
            if (IsDead)
            {
                return false;
            }

            var activated = false;
            foreach (var pair in _grantedSpecs)
            {
                var spec = pair.Value;
                var trigger = spec.Def.TriggerTag;
                if (!trigger.IsValid || trigger.Name != eventData.EventTag)
                {
                    continue;
                }

                var context = new AbilityActivationContext(
                    eventData.TargetLocation,
                    Vector3.forward,
                    eventData.Target);

                var info = new AbilityActivationInfo
                {
                    Instigator = eventData.Instigator,
                    TriggerTarget = eventData.Target,
                    TriggerTag = trigger
                };

                if (TryActivateSpec(spec, context, battle, info, out _).Success)
                {
                    activated = true;
                }
            }

            for (var i = 0; i < _activeInstances.Count; i++)
            {
                var instance = _activeInstances[i];
                if (instance.State == ActiveAbilityState.Active)
                {
                    instance.TaskRunner.HandleGameplayEvent(eventData);
                }
            }

            return activated;
        }

        /// <summary>检查属性 Cost 是否足够，不扣除。</summary>
        /// <param name="costAttributes">消耗表；为空时视为足够。</param>
        /// <returns>可支付返回 true。</returns>
        public bool CanAffordCost(IReadOnlyDictionary<string, float> costAttributes)
        {
            if (costAttributes == null || costAttributes.Count == 0)
            {
                return true;
            }

            foreach (var pair in costAttributes)
            {
                if (Attributes.GetCurrentValue(pair.Key) < pair.Value)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>检查并扣除属性 Cost。</summary>
        public bool TryPayCost(IReadOnlyDictionary<string, float> costAttributes)
        {
            if (!CanAffordCost(costAttributes))
            {
                return false;
            }

            if (costAttributes == null || costAttributes.Count == 0)
            {
                return true;
            }

            foreach (var pair in costAttributes)
            {
                var attr = Attributes.GetOrCreate(pair.Key);
                attr.SetCurrentValue(attr.CurrentValue - pair.Value);
            }

            return true;
        }

        void RevokeGrantedAbilities(List<GameplayAbilitySpecHandle> handles, IEventBus eventBus)
        {
            if (handles == null)
            {
                return;
            }

            for (var i = 0; i < handles.Count; i++)
            {
                RemoveAbility(handles[i], eventBus);
            }
        }

        void RecalculateAttributes(IEventBus eventBus)
        {
            var oldValues = CaptureAttributeValues();
            Attributes.RecalculateAll();
            _aggregator.Clear();

            for (var i = 0; i < _activeEffects.Count; i++)
            {
                var effect = _activeEffects[i];
                var modifiers = effect.Spec.Modifiers;
                for (var j = 0; j < modifiers.Count; j++)
                {
                    var modifier = modifiers[j];
                    var magnitude = modifier.Magnitude.Evaluate(effect.SourceAsc ?? this, this, effect.Spec.SetByCaller);
                    _aggregator.Add(modifier.AttributeName, magnitude, modifier.Operation, effect.StackCount);
                }
            }

            _aggregator.ApplyTo(Attributes);

            var shieldBase = Attributes.GetBaseValue(BattleConstants.Shield);
            _aggregator.TryEvaluate(BattleConstants.Shield, shieldBase, out var shieldMax);
            ApplyDepletableShield(shieldMax);
            ClampToMax(BattleConstants.Health, BattleConstants.MaxHealth);
            ClampToMax(BattleConstants.Mana, BattleConstants.MaxMana);

            PublishAttributeChanges(oldValues, eventBus);

            var healthAttr = Attributes.GetOrCreate(BattleConstants.Health);
            if (healthAttr.CurrentValue <= 0f)
            {
                HandleDeath(eventBus, ActorId.Invalid, null);
            }
        }

        void ApplyDepletableShield(float newMax)
        {
            if (newMax < 0f)
            {
                newMax = 0f;
            }

            var shield = Attributes.GetOrCreate(BattleConstants.Shield);
            var current = shield.CurrentValue;
            if (newMax > _appliedShieldBonus)
            {
                current += newMax - _appliedShieldBonus;
            }

            if (current > newMax)
            {
                current = newMax;
            }

            if (current < 0f)
            {
                current = 0f;
            }

            shield.SetCurrentValue(current);
            _appliedShieldBonus = newMax;
        }

        void ClampToMax(string currentName, string maxName)
        {
            var max = Attributes.GetCurrentValue(maxName);
            var attr = Attributes.GetOrCreate(currentName);
            if (max > 0f && attr.CurrentValue > max)
            {
                attr.SetCurrentValue(max);
            }
        }

        Dictionary<string, float> CaptureAttributeValues()
        {
            var result = new Dictionary<string, float>();
            foreach (var pair in Attributes.GetAllAttributes())
            {
                result[pair.Key] = pair.Value.CurrentValue;
            }

            return result;
        }

        void PublishAttributeChanges(Dictionary<string, float> oldValues, IEventBus eventBus)
        {
            foreach (var pair in Attributes.GetAllAttributes())
            {
                if (!oldValues.TryGetValue(pair.Key, out var oldValue))
                {
                    oldValue = pair.Value.BaseValue;
                }

                var newValue = pair.Value.CurrentValue;
                if (Math.Abs(oldValue - newValue) < 0.0001f)
                {
                    continue;
                }

                eventBus.Publish(new AttributeChangedEvent
                {
                    Actor = ActorId,
                    AttributeName = pair.Key,
                    OldValue = oldValue,
                    NewValue = newValue
                });
            }
        }

        void GrantTags(IReadOnlyList<GameplayTag> tags, IEventBus eventBus)
        {
            for (var i = 0; i < tags.Count; i++)
            {
                if (Tags.AddTag(tags[i]))
                {
                    eventBus.Publish(new TagChangedEvent
                    {
                        Actor = ActorId,
                        Tag = tags[i].Name,
                        Added = true
                    });
                }
            }
        }

        void RemoveTags(IReadOnlyList<GameplayTag> tags, IEventBus eventBus)
        {
            for (var i = 0; i < tags.Count; i++)
            {
                if (Tags.RemoveTag(tags[i]))
                {
                    eventBus.Publish(new TagChangedEvent
                    {
                        Actor = ActorId,
                        Tag = tags[i].Name,
                        Added = false
                    });
                }
            }
        }

        void InterruptIfCrowdControlled(IEventBus eventBus)
        {
            if (Tags.HasTag(new GameplayTag(BattleConstants.TagKnockedDown)))
            {
                CancelAllAbilities(eventBus);
                return;
            }

            if (Tags.HasTag(new GameplayTag(BattleConstants.TagStunned)) &&
                !Tags.HasTag(new GameplayTag(BattleConstants.TagHyperArmor)))
            {
                CancelAllAbilities(eventBus);
            }
        }

        void HandleDeath(IEventBus eventBus, ActorId killer, string abilityId)
        {
            if (IsDead)
            {
                return;
            }

            if (Tags.AddTag(new GameplayTag(BattleConstants.TagDead)))
            {
                eventBus.Publish(new TagChangedEvent
                {
                    Actor = ActorId,
                    Tag = BattleConstants.TagDead,
                    Added = true
                });
            }

            CancelAllAbilities(eventBus);
            eventBus.Publish(new ActorDiedEvent
            {
                Actor = ActorId,
                Killer = killer,
                AbilityId = abilityId
            });
        }

        void CancelInstancesForSpec(GameplayAbilitySpecHandle handle, IEventBus eventBus)
        {
            _activeInstancesScratch.Clear();
            _activeInstancesScratch.AddRange(_activeInstances);
            for (var i = 0; i < _activeInstancesScratch.Count; i++)
            {
                var instance = _activeInstancesScratch[i];
                if (instance.Spec.Handle == handle)
                {
                    CancelAbility(instance, eventBus);
                }
            }
        }

        static bool TagsMatchQuery(IReadOnlyList<GameplayTag> granted, GameplayTag query)
        {
            if (granted == null)
            {
                return false;
            }

            for (var i = 0; i < granted.Count; i++)
            {
                if (GameplayTagMatcher.Matches(granted[i], query))
                {
                    return true;
                }
            }

            return false;
        }

        sealed class NullEventBus : IEventBus
        {
            public static readonly NullEventBus Instance = new NullEventBus();

            public IDisposable Subscribe<T>(Action<T> handler) where T : struct => EmptySubscription.Instance;

            public void Unsubscribe<T>(Action<T> handler) where T : struct { }

            public void Publish<T>(T evt) where T : struct { }

            public void Clear() { }

            sealed class EmptySubscription : IDisposable
            {
                public static readonly EmptySubscription Instance = new EmptySubscription();

                public void Dispose() { }
            }
        }
    }
}
