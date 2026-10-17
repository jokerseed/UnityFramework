namespace Framework.GamePlay
{
    /// <summary>
    /// 模拟逻辑版本号。任何会改变同一输入下逐帧结果的改动（数值公式、Tick 顺序、校验和覆盖范围、定点 LUT 等）都必须把 <see cref="Current"/> 加一。
    /// 录像带和联机握手带上这个版本：版本不同的两端不能对拍，也不能同场联机。
    /// <c>tests/Framework.Sim.Tests</c> 的 golden 录像测试会在逐帧结果变化时失败，提醒同步修改本版本与 golden 值。
    /// </summary>
    public static class BattleSimulationVersion
    {
        /// <summary>当前模拟逻辑版本。</summary>
        /// <remarks>
        /// 1：首个版本。
        /// 2：内置技能与配置数值改为定点并按万分之一量化、实体 ID 带代数、投射物扫掠命中、死亡单位不再结算伤害、周期效果刷新默认不重置计时。
        /// </remarks>
        public const int Current = 2;
    }
}
