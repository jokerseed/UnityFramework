using System.Collections.Generic;
using Framework.Core;
using Framework.GAS.Abilities;
using UnityEngine;

namespace Framework.GAS.Targeting
{
    /// <summary>目标选择器接口，为技能提供灵活可替换的目标查询策略。</summary>
    public interface ITargetSelector
    {
        /// <summary>尝试选择主目标。</summary>
        /// <param name="caster">施放技能的单位 ASC。</param>
        /// <param name="context">激活上下文（含起点、方向、范围）。</param>
        /// <param name="target">选定的目标单位 ID；未选中时为默认值。</param>
        /// <returns>成功选中目标返回 true。</returns>
        bool TrySelectPrimary(
            AbilitySystemComponent caster,
            in AbilityActivationContext context,
            out ActorId target);
    }

    /// <summary>近战：选择主目标或范围内最近敌人（由 Bridge 注入查询）。</summary>
    public sealed class MeleeTargetSelector : ITargetSelector
    {
        readonly System.Func<ActorId, Vector3, float, ActorId> _queryNearestEnemy;

        /// <summary>创建近战目标选择器。</summary>
        /// <param name="queryNearestEnemy">
        /// 查询最近敌人的委托，参数依次为：施法者 ID、世界坐标起点、搜索半径；返回最近敌人 ID（无则为无效值）。
        /// </param>
        public MeleeTargetSelector(System.Func<ActorId, Vector3, float, ActorId> queryNearestEnemy)
        {
            _queryNearestEnemy = queryNearestEnemy;
        }

        /// <summary>优先使用上下文中的主目标；无主目标时查询范围内最近敌人。</summary>
        /// <param name="caster">施放技能的单位 ASC。</param>
        /// <param name="context">激活上下文。</param>
        /// <param name="target">选定的目标 ID。</param>
        /// <returns>成功选中返回 true。</returns>
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
