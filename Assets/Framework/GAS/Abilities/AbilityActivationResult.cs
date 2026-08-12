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

    /// <summary>技能激活结果，包含成功标志与失败原因。</summary>
    public readonly struct AbilityActivationResult
    {
        /// <summary>技能是否成功激活。</summary>
        public bool Success { get; }

        /// <summary>失败原因；激活成功时为 <see cref="AbilityActivationFailureReason.None"/>。</summary>
        public AbilityActivationFailureReason FailureReason { get; }

        AbilityActivationResult(bool success, AbilityActivationFailureReason reason)
        {
            Success = success;
            FailureReason = reason;
        }

        /// <summary>创建表示成功激活的结果。</summary>
        /// <returns>成功结果实例。</returns>
        public static AbilityActivationResult Succeeded() =>
            new AbilityActivationResult(true, AbilityActivationFailureReason.None);

        /// <summary>创建表示激活失败的结果。</summary>
        /// <param name="reason">具体失败原因。</param>
        /// <returns>携带失败原因的结果实例。</returns>
        public static AbilityActivationResult Failed(AbilityActivationFailureReason reason) =>
            new AbilityActivationResult(false, reason);
    }
}
