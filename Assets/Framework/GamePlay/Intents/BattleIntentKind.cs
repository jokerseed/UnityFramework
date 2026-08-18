namespace Framework.GamePlay
{
    /// <summary>
    /// 行为指令类型。玩家与 AI 共用。
    /// </summary>
    public enum BattleIntentKind
    {
        /// <summary>设置水平速度与朝向；速度为零表示停步。</summary>
        Move = 0,

        /// <summary>尝试激活技能。</summary>
        Cast = 1,
    }
}
