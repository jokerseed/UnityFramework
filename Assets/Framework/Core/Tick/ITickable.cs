namespace Framework.Core.Tick
{
    /// <summary>可按帧驱动的 Tick 接口，由战斗主循环统一调用。</summary>
    public interface ITickable
    {
        /// <summary>执行一帧逻辑更新。</summary>
        /// <param name="deltaTime">距上一帧的逻辑时间步长（秒）。</param>
        void Tick(float deltaTime);
    }
}
