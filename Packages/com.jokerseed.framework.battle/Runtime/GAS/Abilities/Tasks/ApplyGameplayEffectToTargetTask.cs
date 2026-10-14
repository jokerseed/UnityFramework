using System;
using Framework.Core;
using Framework.Events;
using Framework.GAS.Effects;

namespace Framework.GAS.Abilities.Tasks
{
    /// <summary>对目标 ASC 施加 GameplayEffect。</summary>
    public sealed class ApplyGameplayEffectToTargetTask : AbilityTask
    {
        readonly GameplayEffectDef _effectDef;
        readonly Func<ActorId> _targetResolver;
        readonly AbilitySystemComponent _ownerAsc;
        readonly Func<ActorId, AbilitySystemComponent> _ascLookup;
        readonly IEventBus _eventBus;

        /// <summary>构造 ApplyGE Task。</summary>
        public ApplyGameplayEffectToTargetTask(
            AbilitySystemComponent ownerAsc,
            GameplayEffectDef effectDef,
            Func<ActorId> targetResolver,
            Func<ActorId, AbilitySystemComponent> ascLookup,
            IEventBus eventBus)
        {
            _ownerAsc = ownerAsc;
            _effectDef = effectDef;
            _targetResolver = targetResolver;
            _ascLookup = ascLookup;
            _eventBus = eventBus;
        }

        /// <inheritdoc/>
        protected override void OnActivate()
        {
            var targetId = _targetResolver();
            if (!targetId.IsValid)
            {
                Finish();
                return;
            }

            var targetAsc = _ascLookup(targetId);
            if (targetAsc == null)
            {
                Finish();
                return;
            }

            targetAsc.ApplyEffect(_effectDef, _ownerAsc.ActorId, _eventBus, Instance.ActivationInfo.SetByCaller, _ownerAsc);
            Finish();
        }
    }
}
