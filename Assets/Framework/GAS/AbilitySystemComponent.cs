using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Core.Commands;
using Framework.Events;
using Framework.GAS.Abilities;
using Framework.GAS.Attributes;
using Framework.GAS.Combat;
using Framework.GAS.Effects;
using Framework.GAS.Events;
using Framework.GAS.Tags;

namespace Framework.GAS
{
    /// <summary>
    /// 技能系统组件（ASC），是 GAS 的核心运行时对象。
    /// 每个战斗单位持有一个 ASC，负责管理技能、属性、标签、持续效果与冷却。
    /// </summary>
    public sealed class AbilitySystemComponent
    {
        readonly Dictionary<string, float> _cooldowns = new Dictionary<string, float>();
        readonly Dictionary<string, GameplayAbility> _abilities = new Dictionary<string, GameplayAbility>();
        readonly List<ActiveGameplayEffect> _activeEffects = new List<ActiveGameplayEffect>();
        readonly List<string> _cooldownKeysScratch = new List<string>();
        readonly AttributeModifierAggregator _aggregator = new AttributeModifierAggregator();

        /// <summary>所属单位的唯一标识符。</summary>
        public ActorId ActorId { get; }

        /// <summary>属性集合（生命值、攻击力等）。</summary>
        public GameplayAttributeSet Attributes { get; } = new GameplayAttributeSet();

        /// <summary>当前持有的 GameplayTag 集合。</summary>
        public GameplayTagContainer Tags { get; } = new GameplayTagContainer();

        /// <summary>伤害处理管线；默认为 <see cref="DefaultDamageProcessor"/>，可替换为自定义实现。</summary>
        public IDamageProcessor DamageProcessor { get; set; } = new DefaultDamageProcessor();

        /// <summary>创建与指定单位绑定的技能系统组件。</summary>
        /// <param name="actorId">所属单位标识符。</param>
        public AbilitySystemComponent(ActorId actorId)
        {
            ActorId = actorId;
        }

        /// <summary>注册技能到本 ASC；同 <see cref="GameplayAbility.AbilityId"/> 会覆盖旧实例。</summary>
        /// <param name="ability">要注册的技能；不可为 null。</param>
        public void RegisterAbility(GameplayAbility ability) => _abilities[ability.AbilityId] = ability;

        /// <summary>尝试激活指定技能。依次检查冷却、标签前提，通过后执行激活并启动冷却。</summary>
        /// <param name="abilityId">技能 ID。</param>
        /// <param name="context">激活上下文（起点、方向、目标、范围）。</param>
        /// <param name="battle">当前帧战斗上下文，用于写入命令缓冲。</param>
        /// <returns>激活结果；失败时携带具体原因。</returns>
        public AbilityActivationResult TryActivateAbility(
            string abilityId,
            in AbilityActivationContext context,
            BattleContext battle)
        {
            if (!_abilities.TryGetValue(abilityId, out var ability))
            {
                return AbilityActivationResult.Failed(AbilityActivationFailureReason.AbilityNotFound);
            }

            var canActivate = ability.CanActivate(this, context);
            if (!canActivate.Success)
            {
                return canActivate;
            }

            ability.Activate(this, context, battle);
            _cooldowns[abilityId] = ability.Cooldown;
            return AbilityActivationResult.Succeeded();
        }

        /// <summary>将 GameplayEffect 应用到本 ASC。瞬时效果直接修改属性；持续效果进入叠加/刷新逻辑。</summary>
        /// <param name="spec">要应用的效果描述；不可为 null。</param>
        /// <param name="source">施加效果的来源单位标识符。</param>
        /// <param name="eventBus">事件总线，用于发布属性变化事件。</param>
        /// <returns>效果是否成功应用；被 Tag 阻止或叠加策略拒绝时返回 false。</returns>
        public bool ApplyEffect(GameplayEffectSpec spec, ActorId source, IEventBus eventBus)
        {
            if (!CanApplyEffect(spec))
            {
                return false;
            }

            if (spec.DurationPolicy == EffectDurationPolicy.Instant)
            {
                ApplyInstantEffect(spec, eventBus);
                return true;
            }

            if (!TryStackOrAddEffect(spec, source, eventBus))
            {
                return false;
            }

            RecalculateAttributes(eventBus);
            return true;
        }

        /// <summary>
        /// 将伤害上下文经 <see cref="DamageProcessor"/> 处理后扣除生命值，并发布相应事件。
        /// 生命值归零时自动添加 Dead 标签并发布 <see cref="Events.TagChangedEvent"/>。
        /// </summary>
        /// <param name="context">原始伤害上下文（来源、目标、原始伤害、技能 ID）。</param>
        /// <param name="eventBus">事件总线，用于发布属性变化、伤害结算、阻断等事件。</param>
        /// <param name="sourceAsc">来源单位的 ASC；用于伤害处理管线获取来源属性，可为 null。</param>
        /// <returns>伤害是否实际生效（未被免疫且最终伤害 &gt; 0）。</returns>
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

        /// <summary>每帧驱动冷却计时与持续效果 Tick，过期效果移除后重新计算属性。</summary>
        /// <param name="deltaTime">帧间隔时间（秒）。</param>
        /// <param name="eventBus">事件总线，用于发布属性变化及 Tag 变化事件。</param>
        public void Tick(float deltaTime, IEventBus eventBus)
        {
            TickCooldowns(deltaTime);
            TickEffects(deltaTime, eventBus);
        }

        /// <summary>获取指定技能的剩余冷却时间（秒）；未在冷却中则返回 0。</summary>
        /// <param name="abilityId">技能 ID。</param>
        /// <returns>剩余冷却秒数；未冷却或未找到则返回 0。</returns>
        public float CooldownRemaining(string abilityId) =>
            _cooldowns.TryGetValue(abilityId, out var remaining) ? remaining : 0f;

        /// <summary>初始化生命属性（MaxHealth 与 Health 均设为 <paramref name="maxHealth"/>）。</summary>
        /// <param name="maxHealth">初始最大生命值；同时作为当前生命值。</param>
        public void InitializeHealth(float maxHealth)
        {
            Attributes.GetOrCreate(BattleConstants.MaxHealth, maxHealth).SetBaseValue(maxHealth);
            Attributes.GetOrCreate(BattleConstants.Health, maxHealth).SetCurrentValue(maxHealth);
        }

        bool CanApplyEffect(GameplayEffectSpec spec)
        {
            if (Tags.HasAny(spec.ApplicationBlockedTags))
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
                        return true;
                    case EffectStackingPolicy.StackCount:
                        existing.StackCount++;
                        existing.RemainingTime = spec.Duration;
                        GrantTags(spec.GrantedTags, eventBus);
                        return true;
                    case EffectStackingPolicy.AggregateBySource:
                        return false;
                }
            }

            var active = new ActiveGameplayEffect(spec, source);
            _activeEffects.Add(active);
            GrantTags(spec.GrantedTags, eventBus);
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

        void ApplyInstantEffect(GameplayEffectSpec spec, IEventBus eventBus)
        {
            for (var i = 0; i < spec.Modifiers.Count; i++)
            {
                var modifier = spec.Modifiers[i];
                var attribute = Attributes.GetOrCreate(modifier.AttributeName);
                var oldValue = attribute.CurrentValue;

                switch (modifier.Operation)
                {
                    case EffectModifierOperation.Add:
                        attribute.SetCurrentValue(oldValue + modifier.Magnitude);
                        break;
                    case EffectModifierOperation.Multiply:
                        attribute.SetCurrentValue(oldValue * modifier.Magnitude);
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
                if (effect.Spec.DurationPolicy != EffectDurationPolicy.Duration)
                {
                    continue;
                }

                effect.RemainingTime -= deltaTime;
                if (!effect.IsExpired)
                {
                    continue;
                }

                RemoveTags(effect.Spec.GrantedTags, eventBus);
                _activeEffects.RemoveAt(i);
                changed = true;
            }

            if (changed)
            {
                RecalculateAttributes(eventBus);
            }
        }

        void RecalculateAttributes(IEventBus eventBus)
        {
            Attributes.RecalculateAll();
            _aggregator.Clear();

            for (var i = 0; i < _activeEffects.Count; i++)
            {
                var effect = _activeEffects[i];
                var modifiers = effect.Spec.Modifiers;
                for (var j = 0; j < modifiers.Count; j++)
                {
                    _aggregator.Add(modifiers[j].AttributeName, modifiers[j].Magnitude, modifiers[j].Operation, effect.StackCount);
                }
            }

            _aggregator.ApplyTo(Attributes);

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
