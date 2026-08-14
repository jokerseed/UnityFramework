namespace Framework.Core
{
    /// <summary>伤害类型，决定走护甲还是魔抗。</summary>
    public enum BattleDamageType
    {
        /// <summary>物理伤害，减免使用防御力。</summary>
        Physical = 0,

        /// <summary>法术伤害，减免使用魔抗。</summary>
        Magical = 1,
    }
}
