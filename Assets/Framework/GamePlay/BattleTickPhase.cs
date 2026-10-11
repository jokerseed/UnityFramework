namespace Framework.GamePlay
{
    /// <summary>
    /// 默认逻辑步阶段的顺序值。自定义步骤用这些值之间的整数插入，不必改 <see cref="GamePlayFramework"/>。
    /// 数值越小越早执行。相同顺序时，先注册的先执行；内置步骤在构造时注册。
    /// </summary>
    public enum BattleTickPhase
    {
        /// <summary>空间索引有结构脏标记时重建。顺序值 0。</summary>
        RebuildSpatialIndex = 0,

        /// <summary>拍下本步 Actor 列表，并把 ECS 位姿同步到 Cue。顺序值 100。</summary>
        SyncCue = 100,

        /// <summary>定身、眩晕、倒地时清速度。顺序值 200。</summary>
        ApplyRootAndStun = 200,

        /// <summary>按 ActorId 驱动存活 ASC。顺序值 300。</summary>
        TickAbilities = 300,

        /// <summary>把生成投射物命令刷进 ECS。顺序值 400。</summary>
        FlushSpawn = 400,

        /// <summary>ECS 世界步进，含物理、空间索引和投射物寿命。顺序值 500。</summary>
        TickEcs = 500,

        /// <summary>刷伤害、治疗、效果、位移等结算命令。顺序值 600。</summary>
        FlushOutcome = 600,

        /// <summary>把 GAS 死亡同步到 ECS。顺序值 700。</summary>
        SyncDeath = 700,

        /// <summary>把 ECS 位置写回 Actor。顺序值 800。</summary>
        SyncPositions = 800
    }

    /// <summary>已注册逻辑步的名称和顺序，供查看管线，不参与模拟。</summary>
    public readonly struct BattleTickStepInfo
    {
        /// <summary>步骤名。</summary>
        public string Name { get; }

        /// <summary>顺序值。</summary>
        public int Order { get; }

        /// <summary>构造一条步骤描述。</summary>
        /// <param name="name">步骤名。</param>
        /// <param name="order">顺序值。</param>
        public BattleTickStepInfo(string name, int order)
        {
            Name = name;
            Order = order;
        }
    }
}
