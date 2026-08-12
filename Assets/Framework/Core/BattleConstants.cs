namespace Framework.Core
{
    /// <summary>战斗框架常量：GAS 属性名、标签字符串与物理参数默认值。</summary>
    public static class BattleConstants
    {
        /// <summary>当前生命值属性名。</summary>
        public const string Health = "Health";

        /// <summary>最大生命值属性名。</summary>
        public const string MaxHealth = "MaxHealth";

        /// <summary>攻击力属性名。</summary>
        public const string Attack = "Attack";

        /// <summary>防御力属性名。</summary>
        public const string Defense = "Defense";

        /// <summary>死亡状态标签。</summary>
        public const string TagDead = "State.Dead";

        /// <summary>眩晕（硬控）状态标签。</summary>
        public const string TagStunned = "State.CrowdControl.Stunned";

        /// <summary>免疫伤害标签。</summary>
        public const string TagImmuneDamage = "Immunity.Damage";

        /// <summary>Actor 碰撞体默认半径（米）。</summary>
        public const float DefaultActorCollisionRadius = 0.5f;

        /// <summary>空间分区格子边长（米），影响宽相位碰撞精度与性能。</summary>
        public const float SpatialCellSize = 2f;
    }
}
