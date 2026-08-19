using Framework.FixedMath;

namespace Framework.Core
{
    /// <summary>
    /// 确定性随机源契约。战斗模拟禁止使用 <c>UnityEngine.Random</c>。
    /// </summary>
    public interface IDeterministicRandom
    {
        /// <summary>返回 [0, 1] 区间内的下一个随机数。</summary>
        /// <returns>单位区间定点随机值。</returns>
        FP Next01();
    }
}
