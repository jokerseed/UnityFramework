using System;

namespace Game
{
    /// <summary>
    /// 战斗流程状态。
    /// </summary>
    public enum BattleFlowState
    {
        /// <summary>
        /// 尚未创建会话。
        /// </summary>
        Idle = 0,

        /// <summary>
        /// 正在准备配置与会话。
        /// </summary>
        Preparing = 1,

        /// <summary>
        /// 会话已创建并处于运行中。
        /// </summary>
        Running = 2,

        /// <summary>
        /// 流程已结束并完成清理。
        /// </summary>
        Exited = 3,

        /// <summary>
        /// 准备流程失败。
        /// </summary>
        Failed = 4,
    }
}
