using Framework.FixedMath;

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

        /// <summary>防御力属性名（物理减伤）。</summary>
        public const string Defense = "Defense";

        /// <summary>魔抗属性名（法术减伤）。</summary>
        public const string MagicDefense = "MagicDefense";

        /// <summary>法力值属性名。</summary>
        public const string Mana = "Mana";

        /// <summary>最大法力属性名。</summary>
        public const string MaxMana = "MaxMana";

        /// <summary>护盾值属性名；伤害优先扣除护盾（可耗尽资源，不参与全量 Modifier 重算覆盖）。</summary>
        public const string Shield = "Shield";

        /// <summary>暴击率属性名（0~1）。</summary>
        public const string CritChance = "CritChance";

        /// <summary>暴击伤害倍率属性名。</summary>
        public const string CritMultiplier = "CritMultiplier";

        /// <summary>承伤倍率属性名；1 为正常，&gt;1 为易伤，&lt;1 为减伤。由 GE Modifier 驱动。</summary>
        public const string IncomingDamageMultiplier = "IncomingDamageMultiplier";

        /// <summary>未配置暴击倍率时的默认值。</summary>
        public static readonly FP DefaultCritMultiplier = (FP)2;

        /// <summary>死亡状态标签。</summary>
        public const string TagDead = "State.Dead";

        /// <summary>眩晕（硬控）状态标签；打断并禁止激活技能。</summary>
        public const string TagStunned = "State.CrowdControl.Stunned";

        /// <summary>沉默状态标签；禁止激活技能。</summary>
        public const string TagSilenced = "State.CrowdControl.Silenced";

        /// <summary>定身状态标签；禁止移动（由 GamePlay 写速度）。</summary>
        public const string TagRooted = "State.CrowdControl.Rooted";

        /// <summary>冷却效果授予的标签，供驱散识别。</summary>
        public const string TagCooldown = "Effect.Cooldown";

        /// <summary>增益效果标签，供驱散识别。</summary>
        public const string TagBuff = "Effect.Buff";

        /// <summary>减益效果标签，供驱散识别。</summary>
        public const string TagDebuff = "Effect.Debuff";

        /// <summary>免疫伤害标签。</summary>
        public const string TagImmuneDamage = "Immunity.Damage";

        /// <summary>近战技能激活中标签，用于连招取消与禁止重叠挥砍。</summary>
        public const string TagMeleeActive = "Ability.Melee.Active";

        /// <summary>近战连招窗口标签，由 ComboWindow 效果授予。</summary>
        public const string TagComboWindow = "Ability.Melee.ComboWindow";

        /// <summary>霸体标签；眩晕不会打断当前技能，倒地仍会打断。</summary>
        public const string TagHyperArmor = "State.HyperArmor";

        /// <summary>倒地标签；禁止移动与技能，且无视霸体打断。</summary>
        public const string TagKnockedDown = "State.CrowdControl.KnockedDown";

        /// <summary>闪避中标签；无敌帧期间禁止用 WASD 覆盖冲刺。</summary>
        public const string TagDodging = "State.Dodging";

        /// <summary>击退冲量默认持续秒数。</summary>
        public static readonly FP DefaultKnockbackDuration = (FP)0.16f;

        /// <summary>冷却效果 ID 前缀，完整 ID 为 <c>Cooldown.{abilityId}</c>。</summary>
        public const string CooldownEffectPrefix = "Cooldown.";

        /// <summary>Actor 碰撞体默认半径（米）。</summary>
        public static readonly FP DefaultActorCollisionRadius = (FP)0.5f;

        /// <summary>空间分区格子边长（米），影响宽相位碰撞精度与性能。</summary>
        public static readonly FP SpatialCellSize = (FP)2;

        /// <summary>默认最大法力。</summary>
        public static readonly FP DefaultMaxMana = (FP)100;

        /// <summary>
        /// 是否为可耗尽资源属性（当前值由伤害/消耗改写，不能被 Modifier 全量重算覆盖）。
        /// </summary>
        /// <param name="attributeName">属性名。</param>
        /// <returns>Health / Mana / Shield 时返回 true。</returns>
        public static bool IsResourceAttribute(string attributeName) =>
            attributeName == Health || attributeName == Mana || attributeName == Shield;
    }
}
