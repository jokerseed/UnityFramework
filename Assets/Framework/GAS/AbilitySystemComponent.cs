using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Core.Commands;
using Framework.Events;
using Framework.GAS.Abilities;
using Framework.GAS.Abilities.Tasks;
using Framework.GAS.Attributes;
using Framework.GAS.Combat;
using Framework.GAS.Effects;
using Framework.GAS.Events;
using Framework.GAS.Tags;

namespace Framework.GAS
{
    /// <summary>
    /// 技能系统组件（ASC），是 GAS 的核心运行时对象。
    /// 每个战斗单位持有一个 ASC，负责管理技能 Spec、属性、标签、持续效果与冷却。
    /// </summary>
    public sealed class AbilitySystemComponent
    {
        int _nextSpecHandle = 1;

        readonly Dictionary<string, float> _cooldowns = new Dictionary<string, float>();
        readonly Dictionary<GameplayAbilitySpecHandle, GameplayAbilitySpec> _grantedSpecs =
            new Dictionary<GameplayAbilitySpecHandle, GameplayAbilitySpec>();
        readonly Dictionary<string, GameplayAbilitySpecHandle> _specByAbilityId =
            new Dictionary<string, GameplayAbilitySpecHandle>();
        readonly List<ActiveAbilityInstance> _activeInstances = new List<ActiveAbilityInstance>();
        readonly List<ActiveGameplayEffect> _activeEffects = new List<ActiveGameplayEffect>();
        readonly List<string> _cooldownKeysScratch = new List<string>();
        readonly List<ActiveAbilityInstance> _activeInstancesScratch = new List<ActiveAbilityInstance>();
        readonly AttributeModifierAggregator _aggregator = new AttributeModifierAggregator();

        /// <summary>所属单位的唯一标识符。</summary>
        public ActorId ActorId { get; }

        /// <summary>属性集合（生命值、攻击力等）。</summary>
        public GameplayAttributeSet Attributes { get; } = new GameplayAttributeSet();

        /// <summary>当前持有的 GameplayTag 集合。</summary>
        public GameplayTagContainer Tags { get; } = new GameplayTagContainer();

        /// <summary>伤害处理管线；默认为 <see cref="DefaultDamageProcessor"/>，可替换为自定义实现。</summary>
        public IDamageProcessor DamageProcessor { get; set; } = new DefaultDamageProcessor();

        /// <summary>当前激活中的技能实例（只读）。</summary>
        public IReadOnlyList<ActiveAbilityInstance> ActiveInstances => _activeInstances;

        /// <summary>创建与指定单位绑定的技能系统组件。</summary>
        /// <param name="actorId">所属单位标识符。</param>
        public AbilitySystemComponent(ActorId actorId)
        {
            ActorId = actorId;
        }

        /// <summary>授予技能并返回 Spec 句柄。</summary>
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

            if (_specByAbilityId.TryGetValue(def.AbilityId, out var existing))
            {
                RemoveAbility(existing);
            }

            var handle = new GameplayAbilitySpecHandle(_nextSpecHandle++);
            var ability = def.CreateAbility();
            var spec = new GameplayAbilitySpec(handle, def, ability, level, inputId);
            _grantedSpecs[handle] = spec;
            _specByAbilityId[def.AbilityId] = handle;
            return handle;
        }

        /// <summary>移除已授予技能。</summary>
        /// <param name="handle">Spec 句柄。</param>
        /// <returns>移除成功返回 true。</returns>
        public bool RemoveAbility(GameplayAbilitySpecHandle handle)
        {
            if (!handle.IsValid || !_grantedSpecs.TryGetValue(handle, out var spec))
            {
                return false;
            }

            _grantedSpecs.Remove(handle);
            _specByAbilityId.Remove(spec.Def.AbilityId);
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

        /// <summary>尝试按技能 ID 激活（兼容 API）。</summary>
        /// <param name="abilityId">技能 ID。</param>
        /// <param name="context">激活上下文。</param>
        /// <param name="battle">战斗上下文。</param>
        /// <returns>激活结果。</returns>
        public AbilityActivationResult TryActivateAbility(
            string abilityId,
            in AbilityActivationContext context,
            BattleContext battle)
        {
            if (!_specByAbilityId.TryGetValue(abilityId, out var handle) ||
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
            instance.Spec.Ability.End(this, instance);
            instance.ClearTasks();
            _activeInstances.Remove(instance);
        }

        /// <summary>启动技能冷却。</summary>
        /// <param name="abilityId">技能 ID。</param>
        /// <param name="cooldown">冷却秒数。</param>
        public void StartCooldown(string abilityId, float cooldown)
        {
            if (cooldown > 0f)
            {
                _cooldowns[abilityId] = cooldown;
            }
        }

        /// <summary>将 GameplayEffect 应用到本 ASC。</summary>
        public bool ApplyEffect(GameplayEffectSpec spec, ActorId source, IEventBus eventBus) =>
            ApplyEffect(spec, source, eventBus, null);

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
            IReadOnlyDictionary<string, float> setByCaller)
        {
            if (!CanApplyEffect(spec))
            {
                return false;
            }

            if (spec.DurationPolicy == EffectDurationPolicy.Instant)
            {
                ApplyInstantEffect(spec, eventBus, setByCaller);
                return true;
            }

            if (!TryStackOrAddEffect(spec, source, eventBus))
            {
                return false;
            }

            RecalculateAttributes(eventBus);
            return true;
        }

        /// <summary>应用 GameplayEffectDef（阶段 2+）。</summary>
        public bool ApplyEffect(
            GameplayEffectDef def,
            ActorId source,
            IEventBus eventBus,
            IReadOnlyDictionary<string, float> setByCaller = null) =>
            ApplyEffect(def.ToRuntimeSpec(setByCaller), source, eventBus, setByCaller);

        /// <summary>将伤害上下文经管线处理后扣除生命值。</summary>
        public bool ApplyDamage(in DamageContext context, IEventBus eventBus, AbilitySystemComponent sourceAsc = null)
        {
            var processed = DamageProcessor.Process(context, sourceAsc, this);
            if (processed.FinalDamage <= 0f)
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
                FinalDamage = processed.FinalDamage
            });

            if (newValue <= 0f && Tags.AddTag(new GameplayTag(BattleConstants.TagDead)))
            {
                eventBus.Publish(new TagChangedEvent
                {
                    Actor = ActorId,
                    Tag = BattleConstants.TagDead,
                    Added = true
                });
            }

            return true;
        }

        /// <summary>每帧驱动激活技能 Task、冷却与效果 Tick。</summary>
        public void Tick(float deltaTime, IEventBus eventBus)
        {
            TickActiveAbilities(deltaTime, eventBus);
            TickCooldowns(deltaTime);
            TickEffects(deltaTime, eventBus);
        }

        /// <summary>获取指定技能剩余冷却。</summary>
        public float CooldownRemaining(string abilityId) =>
            _cooldowns.TryGetValue(abilityId, out var remaining) ? remaining : 0f;

        /// <summary>尝试获取 Spec 句柄。</summary>
        public bool TryGetSpecHandle(string abilityId, out GameplayAbilitySpecHandle handle) =>
            _specByAbilityId.TryGetValue(abilityId, out handle);

        /// <summary>初始化生命属性。</summary>
        public void InitializeHealth(float maxHealth)
        {
            Attributes.GetOrCreate(BattleConstants.MaxHealth, maxHealth).SetBaseValue(maxHealth);
            Attributes.GetOrCreate(BattleConstants.Health, maxHealth).SetCurrentValue(maxHealth);
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

        bool TryStackOrAddEffect(GameplayEffectSpec spec, ActorId source, IEventBus eventBus)
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
                        return true;
                    case EffectStackingPolicy.StackCount:
                        existing.StackCount++;
                        existing.RemainingTime = spec.Duration;
                        existing.PeriodTimer = spec.Period;
                        GrantTags(spec.GrantedTags, eventBus);
                        return true;
                    case EffectStackingPolicy.AggregateBySource:
                        return false;
                }
            }

            var active = new ActiveGameplayEffect(spec, source);
            _activeEffects.Add(active);
            GrantTags(spec.GrantedTags, eventBus);
            GrantAbilities(active);
            PublishCueTags(spec.CueTagsOnApply, eventBus);
            return true;
        }

        static bool IsSameStackGroup(ActiveGameplayEffect existing, GameplayEffectSpec spec, ActorId source)
        {
            if (existing.Spec.EffectId != spec.EffectId)
            {
                return false;
            }

            return spec.StackingPolicy != EffectStackingPolicy.AggregateBySource || existing.Source == source;
        }

        void ApplyInstantEffect(
            GameplayEffectSpec spec,
            IEventBus eventBus,
            IReadOnlyDictionary<string, float> setByCaller)
        {
            if (spec.Executions != null && spec.Executions.Count > 0)
            {
                var execContext = new ExecutionContext(this, this, spec, setByCaller, eventBus);
                for (var i = 0; i < spec.Executions.Count; i++)
                {
                    spec.Executions[i].Execute(execContext);
                }

                return;
            }

            for (var i = 0; i < spec.Modifiers.Count; i++)
            {
                ApplyModifierInstant(spec.Modifiers[i], eventBus, setByCaller);
            }
        }

        void ApplyModifierInstant(
            EffectModifier modifier,
            IEventBus eventBus,
            IReadOnlyDictionary<string, float> setByCaller)
        {
            var magnitude = modifier.Magnitude.Evaluate(this, this, setByCaller);
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
        }

        void TickCooldowns(float deltaTime)
        {
            if (_cooldowns.Count == 0)
            {
                return;
            }

            _cooldownKeysScratch.Clear();
            foreach (var key in _cooldowns.Keys)
            {
                _cooldownKeysScratch.Add(key);
            }

            for (var i = 0; i < _cooldownKeysScratch.Count; i++)
            {
                var key = _cooldownKeysScratch[i];
                _cooldowns[key] = Math.Max(0f, _cooldowns[key] - deltaTime);
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
            if (effect.Spec.Executions != null && effect.Spec.Executions.Count > 0)
            {
                var execContext = new ExecutionContext(this, this, effect.Spec, null, eventBus);
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
            RevokeGrantedAbilities(effect.Spec.GrantedAbilityDefs, effect.GrantedAbilityHandles);
            PublishCueTags(effect.Spec.CueTagsOnRemove, eventBus);
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

        void PublishCueTags(IReadOnlyList<string> cueTags, IEventBus eventBus)
        {
            if (cueTags == null || cueTags.Count == 0)
            {
                return;
            }

            for (var i = 0; i < cueTags.Count; i++)
            {
                eventBus.Publish(new GameplayCueEvent
                {
                    Actor = ActorId,
                    CueTag = cueTags[i],
                    Position = UnityEngine.Vector3.zero,
                    Direction = UnityEngine.Vector3.forward
                });
            }
        }

        /// <summary>处理 GameplayEvent，尝试触发匹配 TriggerTag 的被动技能。</summary>
        public bool HandleGameplayEvent(in GameplayEventData eventData, BattleContext battle)
        {
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
                    UnityEngine.Vector3.forward,
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

            return activated;
        }

        /// <summary>检查并扣除属性 Cost。</summary>
        public bool TryPayCost(IReadOnlyDictionary<string, float> costAttributes)
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

            foreach (var pair in costAttributes)
            {
                var attr = Attributes.GetOrCreate(pair.Key);
                attr.SetCurrentValue(attr.CurrentValue - pair.Value);
            }

            return true;
        }

        void RevokeGrantedAbilities(IReadOnlyList<GameplayAbilityDef> defs, List<GameplayAbilitySpecHandle> handles)
        {
            if (handles == null)
            {
                return;
            }

            for (var i = 0; i < handles.Count; i++)
            {
                RemoveAbility(handles[i]);
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
                    var magnitude = modifier.Magnitude.Evaluate(this, this, null);
                    _aggregator.Add(modifier.AttributeName, magnitude, modifier.Operation, effect.StackCount);
                }
            }

            _aggregator.ApplyTo(Attributes);
            PublishAttributeChanges(oldValues, eventBus);

            var maxHealth = Attributes.GetCurrentValue(BattleConstants.MaxHealth);
            var healthAttr = Attributes.GetOrCreate(BattleConstants.Health);
            if (healthAttr.CurrentValue > maxHealth)
            {
                var old = healthAttr.CurrentValue;
                healthAttr.SetCurrentValue(maxHealth);
                eventBus.Publish(new AttributeChangedEvent
                {
                    Actor = ActorId,
                    AttributeName = BattleConstants.Health,
                    OldValue = old,
                    NewValue = maxHealth
                });
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
    }
}
