using System;
using System.Collections.Generic;
using Framework.FixedMath;

namespace Framework.GamePlay
{
    /// <summary>一逻辑帧的可回放快照：指令副本 + 模拟后校验和。</summary>
    public readonly struct BattleReplayFrame
    {
        /// <summary>逻辑帧号。</summary>
        public int FrameIndex { get; }

        /// <summary>本帧玩家与 AI 的行为指令副本。</summary>
        public IReadOnlyList<BattleIntentCommand> Commands { get; }

        /// <summary>该帧模拟（含刷波）结束后的校验和。</summary>
        public long Checksum { get; }

        /// <summary>构造一帧录像。</summary>
        /// <param name="frameIndex">逻辑帧号。</param>
        /// <param name="commands">指令副本；不可为 null。</param>
        /// <param name="checksum">模拟后校验和。</param>
        public BattleReplayFrame(int frameIndex, BattleIntentCommand[] commands, long checksum)
        {
            FrameIndex = frameIndex;
            Commands = commands ?? Array.Empty<BattleIntentCommand>();
            Checksum = checksum;
        }
    }

    /// <summary>一场战斗的意图录像：种子 + 固定步长 + 逐帧指令。</summary>
    public sealed class BattleReplayTape
    {
        /// <summary>创建会话时使用的随机种子。</summary>
        public int Seed { get; }

        /// <summary>逻辑固定步长（秒，定点）。</summary>
        public FP FixedDeltaTime { get; }

        /// <summary>按帧号递增的录像。</summary>
        public IReadOnlyList<BattleReplayFrame> Frames { get; }

        /// <summary>构造录像带。</summary>
        /// <param name="seed">随机种子。</param>
        /// <param name="fixedDeltaTime">固定步长（定点）。</param>
        /// <param name="frames">帧列表；不可为 null。</param>
        public BattleReplayTape(int seed, FP fixedDeltaTime, IReadOnlyList<BattleReplayFrame> frames)
        {
            Seed = seed;
            FixedDeltaTime = fixedDeltaTime;
            Frames = frames ?? Array.Empty<BattleReplayFrame>();
        }
    }
}
