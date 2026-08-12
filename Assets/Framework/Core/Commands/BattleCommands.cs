using UnityEngine;

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
        public Vector3 Position;

        /// <summary>投射物的飞行方向（单位向量）。</summary>
        public Vector3 Direction;

        /// <summary>投射物飞行速度（米/秒）。</summary>
        public float Speed;

        /// <summary>投射物碰撞体半径（米）。</summary>
        public float Radius;

        /// <summary>投射物存活时间（秒）；超时自动销毁。</summary>
        public float Lifetime;

        /// <summary>命中时对目标造成的伤害基础值。</summary>
        public float Damage;

        /// <summary>发射者所属队伍 ID，用于友伤判定。</summary>
        public int TeamId;
    }

    /// <summary>对目标施加伤害的命令，由碰撞/技能系统写入 <see cref="BattleCommandBuffer"/>，GAS 系统消费。</summary>
    public struct ApplyDamageCommand
    {
        /// <summary>造成伤害的来源 Actor 标识符。</summary>
        public ActorId Source;

        /// <summary>受到伤害的目标 Actor 标识符。</summary>
        public ActorId Target;

        /// <summary>伤害量（经来源属性计算后的最终值）。</summary>
        public float Damage;

        /// <summary>触发伤害的技能 ID，用于 GAS 效果查表。</summary>
        public string AbilityId;
    }
}
