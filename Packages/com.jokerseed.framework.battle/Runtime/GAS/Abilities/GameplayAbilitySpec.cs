namespace Framework.GAS.Abilities
{
    /// <summary>ASC 上已授予的技能运行时 Spec。</summary>
    public sealed class GameplayAbilitySpec
    {
        /// <summary>稳定句柄。</summary>
        public GameplayAbilitySpecHandle Handle { get; }

        /// <summary>技能定义。</summary>
        public GameplayAbilityDef Def { get; }

        /// <summary>技能运行时实例。</summary>
        public GameplayAbility Ability { get; }

        /// <summary>技能等级。</summary>
        public int Level { get; }

        /// <summary>输入绑定 ID；-1 表示未绑定。</summary>
        public int InputId { get; }

        /// <summary>构造授予 Spec。</summary>
        /// <param name="handle">Spec 句柄。</param>
        /// <param name="def">技能定义。</param>
        /// <param name="ability">运行时技能实例。</param>
        /// <param name="level">技能等级。</param>
        /// <param name="inputId">输入绑定 ID。</param>
        internal GameplayAbilitySpec(
            GameplayAbilitySpecHandle handle,
            GameplayAbilityDef def,
            GameplayAbility ability,
            int level,
            int inputId)
        {
            Handle = handle;
            Def = def;
            Ability = ability;
            Level = level;
            InputId = inputId;
        }
    }
}
