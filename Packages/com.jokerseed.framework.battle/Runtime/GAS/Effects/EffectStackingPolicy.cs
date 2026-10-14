namespace Framework.GAS.Effects
{
    /// <summary>同 EffectId 的多个实例如何叠加。</summary>
    public enum EffectStackingPolicy
    {
        /// <summary>同 EffectId 不可重复，忽略新实例。</summary>
        None,

        /// <summary>同 EffectId 刷新持续时间。</summary>
        RefreshDuration,

        /// <summary>同 EffectId 叠加层数（修改器幅度按层数累加）。</summary>
        StackCount,

        /// <summary>同 Source + EffectId 独立存在。</summary>
        AggregateBySource
    }
}
