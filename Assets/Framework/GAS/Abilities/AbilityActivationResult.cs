namespace Framework.GAS.Abilities
{
    public enum AbilityActivationFailureReason
    {
        None,
        AbilityNotFound,
        OnCooldown,
        MissingRequiredTags,
        HasBlockingTags,
        InvalidTarget,
        InsufficientResource,
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
