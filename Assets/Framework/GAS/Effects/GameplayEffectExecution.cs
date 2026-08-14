using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Events;
using Framework.GAS.Abilities;
using Framework.GAS.Tags;

namespace Framework.GAS.Effects
{
    /// <summary>GameplayEffect 执行上下文。</summary>
    public readonly struct ExecutionContext
    {
        /// <summary>效果来源 ASC。</summary>
        public AbilitySystemComponent Source { get; }

        /// <summary>效果目标 ASC。</summary>
        public AbilitySystemComponent Target { get; }

        /// <summary>效果 Spec。</summary>
        public GameplayEffectSpec Spec { get; }

        /// <summary>SetByCaller 幅度。</summary>
        public IReadOnlyDictionary<string, float> SetByCaller { get; }

        /// <summary>事件总线。</summary>
        public IEventBus EventBus { get; }

        /// <summary>构造 Execution 上下文。</summary>
        public ExecutionContext(
            AbilitySystemComponent source,
            AbilitySystemComponent target,
            GameplayEffectSpec spec,
            IReadOnlyDictionary<string, float> setByCaller,
            IEventBus eventBus)
        {
            Source = source;
            Target = target;
            Spec = spec;
            SetByCaller = setByCaller;
            EventBus = eventBus;
        }
    }

    /// <summary>GameplayEffect 自定义执行逻辑基类（对应 UE ExecutionCalculation）。</summary>
    public abstract class GameplayEffectExecution
    {
        /// <summary>执行效果逻辑。</summary>
        /// <param name="context">执行上下文。</param>
        public abstract void Execute(in ExecutionContext context);
    }

    /// <summary>默认伤害 Execution：SetByCaller/Data 或 Attack 参与计算。</summary>
    public sealed class DamageExecution : GameplayEffectExecution
    {
        readonly float _attackScale;
        readonly string _setByCallerKey;
        readonly BattleDamageType _damageType;

        /// <summary>构造伤害 Execution。</summary>
        /// <param name="attackScale">攻击力缩放系数。</param>
        /// <param name="setByCallerKey">优先使用的 SetByCaller 键；为 null 时使用 Attack 属性。</param>
        /// <param name="damageType">伤害类型。</param>
        public DamageExecution(
            float attackScale = 1f,
            string setByCallerKey = "Damage",
            BattleDamageType damageType = BattleDamageType.Physical)
        {
            _attackScale = attackScale;
            _setByCallerKey = setByCallerKey;
            _damageType = damageType;
        }

        /// <inheritdoc/>
        public override void Execute(in ExecutionContext context)
        {
            var raw = 0f;
            if (context.SetByCaller != null &&
                !string.IsNullOrEmpty(_setByCallerKey) &&
                context.SetByCaller.TryGetValue(_setByCallerKey, out var callerDamage))
            {
                raw = callerDamage;
            }
            else if (context.Source != null)
            {
                raw = context.Source.Attributes.GetCurrentValue(BattleConstants.Attack) * _attackScale;
            }

            var damageContext = new Combat.DamageContext(
                context.Source?.ActorId ?? ActorId.Invalid,
                context.Target.ActorId,
                raw,
                context.Spec?.EffectId ?? "Execution",
                damageType: _damageType);

            context.Target.ApplyDamage(damageContext, context.EventBus, context.Source);
        }
    }

    /// <summary>治疗 Execution：增加 Health。</summary>
    public sealed class HealExecution : GameplayEffectExecution
    {
        readonly ModifierMagnitude _magnitude;

        /// <summary>构造治疗 Execution。</summary>
        /// <param name="magnitude">治疗幅度。</param>
        public HealExecution(ModifierMagnitude magnitude) => _magnitude = magnitude;

        /// <inheritdoc/>
        public override void Execute(in ExecutionContext context)
        {
            var amount = _magnitude.Evaluate(context.Source, context.Target, context.SetByCaller);
            var health = context.Target.Attributes.GetOrCreate(BattleConstants.Health);
            var old = health.CurrentValue;
            var max = context.Target.Attributes.GetCurrentValue(BattleConstants.MaxHealth);
            var next = Math.Min(max, old + amount);
            health.SetCurrentValue(next);

            context.EventBus.Publish(new Events.AttributeChangedEvent
            {
                Actor = context.Target.ActorId,
                AttributeName = BattleConstants.Health,
                OldValue = old,
                NewValue = next
            });
        }
    }

    /// <summary>对目标施加另一个 GameplayEffectDef。</summary>
    public sealed class ApplyGameplayEffectExecution : GameplayEffectExecution
    {
        readonly GameplayEffectDef _effectDef;

        /// <summary>构造施加 GE Execution。</summary>
        /// <param name="effectDef">要施加的效果定义。</param>
        public ApplyGameplayEffectExecution(GameplayEffectDef effectDef) => _effectDef = effectDef;

        /// <inheritdoc/>
        public override void Execute(in ExecutionContext context)
        {
            if (_effectDef == null)
            {
                return;
            }

            context.Target.ApplyEffect(
                _effectDef,
                context.Source?.ActorId ?? ActorId.Invalid,
                context.EventBus,
                context.SetByCaller,
                context.Source);
        }
    }
}
