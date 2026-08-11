using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Core.Commands;
using Framework.Core.Events;
using Framework.GAS.Abilities;
using Framework.GAS.Attributes;
using Framework.GAS.Combat;
using Framework.GAS.Effects;
using Framework.GAS.Tags;

namespace Framework.GAS
{
    public sealed class AbilitySystemComponent
    {
        readonly Dictionary<string, float> _cooldowns = new Dictionary<string, float>();
        readonly Dictionary<string, GameplayAbility> _abilities = new Dictionary<string, GameplayAbility>();
        readonly List<ActiveGameplayEffect> _activeEffects = new List<ActiveGameplayEffect>();
        readonly List<string> _cooldownKeysScratch = new List<string>();
        readonly AttributeModifierAggregator _aggregator = new AttributeModifierAggregator();

        public ActorId ActorId { get; }
        public GameplayAttributeSet Attributes { get; } = new GameplayAttributeSet();
        public GameplayTagContainer Tags { get; } = new GameplayTagContainer();
        public IDamageProcessor DamageProcessor { get; set; } = new DefaultDamageProcessor();

        public AbilitySystemComponent(ActorId actorId)
        {
            ActorId = actorId;
        }

        public void RegisterAbility(GameplayAbility ability) => _abilities[ability.AbilityId] = ability;

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

        public void Tick(float deltaTime, IEventBus eventBus)
        {
            TickCooldowns(deltaTime);
            TickEffects(deltaTime, eventBus);
        }

        public float CooldownRemaining(string abilityId) =>
            _cooldowns.TryGetValue(abilityId, out var remaining) ? remaining : 0f;

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
