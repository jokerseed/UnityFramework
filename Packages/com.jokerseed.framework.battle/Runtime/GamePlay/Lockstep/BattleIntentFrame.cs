using System.Collections.Generic;

namespace Framework.GamePlay
{
    /// <summary>
    /// 一个逻辑帧要执行的行为意图集合。与 <see cref="Framework.Core.Commands.BattleCommandBuffer"/> 分离。
    /// </summary>
    public sealed class BattleIntentFrame
    {
        readonly List<BattleIntentCommand> _commands = new List<BattleIntentCommand>(8);

        /// <summary>逻辑帧号，从 0 递增。</summary>
        public int FrameIndex { get; set; }

        /// <summary>本帧全部行为指令。</summary>
        public List<BattleIntentCommand> Commands => _commands;

        /// <summary>清空指令，准备复用。</summary>
        public void Reset()
        {
            FrameIndex = 0;
            _commands.Clear();
        }
    }
}
