using System.Collections.Generic;
using Framework.FixedMath;
using Framework.Logging;

namespace Framework.GamePlay
{
    /// <summary>
    /// 把已执行的意图帧连同校验和追加到内存录像。
    /// 录像必须从第 0 帧开始连续才能对拍，所以不做环形覆盖：达到 <see cref="MaxFrames"/> 后停止录制并告警一次。
    /// </summary>
    public sealed class BattleReplayRecorder
    {
        /// <summary>默认最多录制的帧数（20Hz 下约 30 分钟）。</summary>
        public const int DefaultMaxFrames = 36000;

        readonly List<BattleReplayFrame> _frames = new List<BattleReplayFrame>(256);

        /// <summary>已录制帧数。</summary>
        public int FrameCount => _frames.Count;

        /// <summary>最多录制的帧数；小于等于 0 表示不限制。</summary>
        public int MaxFrames { get; set; } = DefaultMaxFrames;

        /// <summary>是否因达到 <see cref="MaxFrames"/> 而停止了录制；此时录像不完整，只能对拍已录下的前段。</summary>
        public bool IsTruncated { get; private set; }

        /// <summary>复制一帧指令并记下校验和。</summary>
        /// <param name="frame">已填充的意图帧；不可为 null。</param>
        /// <param name="checksum">该帧模拟结束后的校验和。</param>
        public void Capture(BattleIntentFrame frame, long checksum)
        {
            if (frame == null || IsTruncated)
            {
                return;
            }

            if (MaxFrames > 0 && _frames.Count >= MaxFrames)
            {
                IsTruncated = true;
                GameLog.Warning(LogCategories.GamePlay, $"Replay recorder reached {MaxFrames} frames; recording stopped.");
                return;
            }

            var copy = new BattleIntentCommand[frame.Commands.Count];
            for (var i = 0; i < frame.Commands.Count; i++)
            {
                copy[i] = frame.Commands[i];
            }

            _frames.Add(new BattleReplayFrame(frame.FrameIndex, copy, checksum));
        }

        /// <summary>导出不可变录像快照（版本记为 <see cref="BattleSimulationVersion.Current"/>），后续录制不影响该快照。</summary>
        /// <param name="seed">本场随机种子。</param>
        /// <param name="fixedDeltaTime">固定步长（定点）。</param>
        /// <returns>录像带。</returns>
        public BattleReplayTape ToTape(int seed, FP fixedDeltaTime)
        {
            return new BattleReplayTape(seed, fixedDeltaTime, _frames.ToArray());
        }

        /// <summary>清空已录制帧。</summary>
        public void Clear()
        {
            _frames.Clear();
            IsTruncated = false;
        }
    }
}
