using Framework.FixedMath;

namespace Framework.Core.Commands
{
    /// <summary>生成投射物的命令，由 GAS 技能写入 <see cref="BattleCommandBuffer"/>，ECS 系统消费。</summary>
    public struct SpawnProjectileCommand
    {
        /// <summary>发射该投射物的 Actor 标识符。</summary>
        public ActorId Owner;

        /// <summary>触发生成的技能 ID，用于查表获取投射物配置。</summary>
        public string AbilityId;

        /// <summary>投射物的初始世界坐标。</summary>
        public TSVector Position;

        /// <summary>投射物的飞行方向（单位向量）。</summary>
        public TSVector Direction;

        /// <summary>投射物飞行速度（米/秒）。</summary>
        public FP Speed;

        /// <summary>投射物碰撞体半径（米）。</summary>
        public FP Radius;

        /// <summary>投射物存活时间（秒）；超时自动销毁。</summary>
        public FP Lifetime;

        /// <summary>命中时对目标造成的伤害基础值。</summary>
        public FP Damage;

        /// <summary>发射者所属队伍 ID，用于友伤判定。</summary>
        public int TeamId;

        /// <summary>可穿透的额外命中次数；0 表示命中即销毁。</summary>
        public int PierceCount;

        /// <summary>命中后对目标施加的效果 ID；空则不施加。</summary>
        public string HitEffectId;

        /// <summary>命中爆炸半径；≤0 表示不爆炸。</summary>
        public FP ExplodeRadius;

        /// <summary>伤害类型。</summary>
        public BattleDamageType DamageType;
    }

    /// <summary>对目标施加伤害的命令，由碰撞/技能系统写入 <see cref="BattleCommandBuffer"/>，GAS 系统消费。</summary>
    public struct ApplyDamageCommand
    {
        /// <summary>造成伤害的来源 Actor 标识符。</summary>
        public ActorId Source;

        /// <summary>受到伤害的目标 Actor 标识符。</summary>
        public ActorId Target;

        /// <summary>伤害量（经来源属性计算后的最终值）。</summary>
        public FP Damage;

        /// <summary>触发伤害的技能 ID，用于 GAS 效果查表。</summary>
        public string AbilityId;

        /// <summary>伤害类型。</summary>
        public BattleDamageType DamageType;
    }

    /// <summary>对目标施加治疗。</summary>
    public struct ApplyHealCommand
    {
        /// <summary>治疗来源。</summary>
        public ActorId Source;

        /// <summary>治疗目标。</summary>
        public ActorId Target;

        /// <summary>治疗量。</summary>
        public FP Amount;
    }

    /// <summary>对目标施加 GameplayEffect（由 GamePlay 按 EffectId 装配）。</summary>
    public struct ApplyEffectCommand
    {
        /// <summary>效果来源。</summary>
        public ActorId Source;

        /// <summary>效果目标。</summary>
        public ActorId Target;

        /// <summary>效果配置 ID。</summary>
        public string EffectId;
    }

    /// <summary>对目标施加位移（击退）。</summary>
    public struct ApplyDisplaceCommand
    {
        /// <summary>位移目标。</summary>
        public ActorId Target;

        /// <summary>世界空间位移向量（定点）。</summary>
        public TSVector Offset;
    }

    /// <summary>范围伤害/效果，由投射物爆炸或技能写入。</summary>
    public struct ApplyAreaEffectCommand
    {
        /// <summary>来源 Actor。</summary>
        public ActorId Source;

        /// <summary>圆心。</summary>
        public TSVector Origin;

        /// <summary>半径。</summary>
        public FP Radius;

        /// <summary>对每个目标的伤害；0 表示只施加效果。</summary>
        public FP Damage;

        /// <summary>技能 ID。</summary>
        public string AbilityId;

        /// <summary>对每个目标施加的效果 ID；空则不施加。</summary>
        public string EffectId;

        /// <summary>来源队伍，用于敌对过滤。</summary>
        public int TeamId;

        /// <summary>伤害类型。</summary>
        public BattleDamageType DamageType;

        /// <summary>扇形半角（度）；≤0 表示圆形范围。</summary>
        public FP HalfAngleDegrees;

        /// <summary>扇形朝向；圆形时可忽略。</summary>
        public TSVector Direction;
    }
}
