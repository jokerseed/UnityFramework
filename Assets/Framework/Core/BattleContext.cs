using Framework.Core.Commands;
using Framework.Core.Events;

namespace Framework.Core
{
    /// <summary>
    /// 战斗上下文：命令缓冲（模拟热路径）+ 表现事件总线（Cue/UI）。
    /// </summary>
    public sealed class BattleContext
    {
        public BattleCommandBuffer Commands { get; }
        public IEventBus Presentation { get; }

        public BattleContext(BattleCommandBuffer commands, IEventBus presentation)
        {
            Commands = commands;
            Presentation = presentation;
        }
    }
}
