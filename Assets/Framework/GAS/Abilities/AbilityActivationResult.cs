namespace Framework.GAS.Abilities
{
    /// <summary>技能激活失败原因。</summary>
    public enum AbilityActivationFailureReason
    {
        /// <summary>无错误（激活成功）。</summary>
        None,

        /// <summary>未找到对应技能。</summary>
        AbilityNotFound,

        /// <summary>技能处于冷却中。</summary>
        OnCooldown,

        /// <summary>缺少必需的 GameplayTag。</summary>
        MissingRequiredTags,

        /// <summary>存在阻止激活的 GameplayTag。</summary>
        HasBlockingTags,

        /// <summary>目标无效或不存在。</summary>
        InvalidTarget,

        /// <summary>资源不足（如法力、体力）。</summary>
        InsufficientResource,

        /// <summary>被自定义逻辑拦截。</summary>
        CustomBlocked
    }

    public readonly struct AbilityActivationResult
    {
        public bool Success { get; }
        public AbilityActivationFailureReason FailureReason { get; }

        AbilityActivationResult(bool success, AbilityActivationFailureReason reason)
        {
            Success = success;
            FailureReason = reason;
        }

        public static AbilityActivationResult Succeeded() =>
            new AbilityActivationResult(true, AbilityActivationFailureReason.None);

        public static AbilityActivationResult Failed(AbilityActivationFailureReason reason) =>
            new AbilityActivationResult(false, reason);
    }
}
