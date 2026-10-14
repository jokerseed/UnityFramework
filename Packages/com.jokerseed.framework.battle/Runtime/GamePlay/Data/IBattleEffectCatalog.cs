using Framework.GAS.Effects;

namespace Framework.GamePlay.Data
{
    /// <summary>
    /// 效果目录：按效果 ID 创建 <see cref="GameplayEffectDef"/>。
    /// 不包含配置表类型，Luban 与其它后端都实现本接口。
    /// </summary>
    public interface IBattleEffectCatalog
    {
        /// <summary>尝试创建效果定义。</summary>
        /// <param name="effectId">效果 ID。</param>
        /// <param name="effect">创建成功时的定义；失败时为 null。</param>
        /// <returns>目录中存在该 ID 时返回 true。</returns>
        bool TryCreateEffect(string effectId, out GameplayEffectDef effect);
    }
}
