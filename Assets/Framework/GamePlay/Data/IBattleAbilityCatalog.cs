using Framework.GAS.Abilities;

namespace Framework.GamePlay.Data
{
    /// <summary>
    /// 技能目录：按技能 ID 与队伍创建 <see cref="GameplayAbilityDef"/>。
    /// 不包含配置表类型，Luban 与其它后端都实现本接口。
    /// </summary>
    public interface IBattleAbilityCatalog
    {
        /// <summary>尝试创建技能定义。</summary>
        /// <param name="abilityId">技能 ID。</param>
        /// <param name="teamId">所属队伍；投射物与范围技能用它过滤命中。</param>
        /// <param name="ability">创建成功时的定义；失败时为 null。</param>
        /// <returns>目录中存在该 ID 时返回 true。</returns>
        bool TryCreateAbility(string abilityId, int teamId, out GameplayAbilityDef ability);
    }
}
