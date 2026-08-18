namespace Framework.GamePlay
{
    /// <summary>
    /// 行为指令来源。仅用于日志与回放过滤，不改变执行规则。
    /// </summary>
    public enum BattleIntentSource
    {
        /// <summary>本地玩家采样。</summary>
        Player = 0,

        /// <summary>行为树 / AI 产出。</summary>
        Ai = 1,

        /// <summary>录像或锁步回放注入。</summary>
        Replay = 2,
    }
}
