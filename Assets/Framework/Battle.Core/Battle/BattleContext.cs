using Framework.Core.Commands;
using Framework.Events;

namespace Framework.Core
{
    /// <summary>
    /// 战斗上下文：命令缓冲（模拟热路径）+ 表现事件总线（Cue/UI）。
    /// </summary>
    public sealed class BattleContext
    {
        /// <summary>获取本次战斗的命令缓冲，用于模拟热路径的批量指令。</summary>
        public BattleCommandBuffer Commands { get; }

        /// <summary>获取表现层事件总线，用于 Cue、UI 等非模拟通知。</summary>
        public IEventBus Presentation { get; }

        /// <summary>本场战斗的确定性随机源；未注入时为 null。</summary>
        public IDeterministicRandom Random { get; }

        /// <summary>使用指定命令缓冲与表现事件总线构造战斗上下文。</summary>
        /// <param name="commands">命令缓冲实例，不可为 null。</param>
        /// <param name="presentation">表现层事件总线实例，不可为 null。</param>
        /// <param name="random">确定性随机源；可为 null（暴击等随机效果将不触发）。</param>
        public BattleContext(
            BattleCommandBuffer commands,
            IEventBus presentation,
            IDeterministicRandom random = null)
        {
            Commands = commands;
            Presentation = presentation;
            Random = random;
        }
    }
}
