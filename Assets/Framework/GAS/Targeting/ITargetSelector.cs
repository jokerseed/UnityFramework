using System.Collections.Generic;
using Framework.Core;
using Framework.GAS.Abilities;
using UnityEngine;

namespace Framework.GAS.Targeting
{
    public interface ITargetSelector
    {
        bool TrySelectPrimary(
            AbilitySystemComponent caster,
            in AbilityActivationContext context,
            out ActorId target);
    }

    /// <summary>近战：选择主目标或范围内最近敌人（由 Bridge 注入查询）。</summary>
    public sealed class MeleeTargetSelector : ITargetSelector
    {
        readonly System.Func<ActorId, Vector3, float, ActorId> _queryNearestEnemy;

        public MeleeTargetSelector(System.Func<ActorId, Vector3, float, ActorId> queryNearestEnemy)
        {
            _queryNearestEnemy = queryNearestEnemy;
        }

        public bool TrySelectPrimary(
            AbilitySystemComponent caster,
            in AbilityActivationContext context,
            out ActorId target)
        {
            if (context.PrimaryTarget.IsValid)
            {
                target = context.PrimaryTarget;
                return true;
            }

            target = _queryNearestEnemy(caster.ActorId, context.Origin, context.Range);
            return target.IsValid;
        }
    }
}
