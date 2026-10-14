using System.Collections.Generic;
using Framework.FixedMath;

namespace Framework.GamePlay
{
    /// <summary>把已执行的意图帧连同校验和追加到内存录像。</summary>
    public sealed class BattleReplayRecorder
    {
        readonly List<BattleReplayFrame> _frames = new List<BattleReplayFrame>(256);

        /// <summary>已录制帧数。</summary>
        public int FrameCount => _frames.Count;

        /// <summary>复制一帧指令并记下校验和。</summary>
        /// <param name="frame">已填充的意图帧；不可为 null。</param>
        /// <param name="checksum">该帧模拟结束后的校验和。</param>
        public void Capture(BattleIntentFrame frame, long checksum)
        {
            if (frame == null)
            {
                return;
            }

            var copy = new BattleIntentCommand[frame.Commands.Count];
            for (var i = 0; i < frame.Commands.Count; i++)
            {
                copy[i] = frame.Commands[i];
            }

            _frames.Add(new BattleReplayFrame(frame.FrameIndex, copy, checksum));
        }

        /// <summary>导出不可变录像快照，后续录制不影响该快照。</summary>
        /// <param name="seed">本场随机种子。</param>
        /// <param name="fixedDeltaTime">固定步长（定点）。</param>
        /// <returns>录像带。</returns>
        public BattleReplayTape ToTape(int seed, FP fixedDeltaTime)
        {
            return new BattleReplayTape(seed, fixedDeltaTime, _frames.ToArray());
        }

        /// <summary>清空已录制帧。</summary>
        public void Clear() => _frames.Clear();
    }
}
