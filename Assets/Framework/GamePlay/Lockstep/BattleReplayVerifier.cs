using System;

namespace Framework.GamePlay
{
    /// <summary>本地对拍结果。</summary>
    public readonly struct BattleReplayVerifyResult
    {
        /// <summary>全部帧校验和一致。</summary>
        public bool Matched { get; }

        /// <summary>实际比较的帧数。</summary>
        public int ComparedFrames { get; }

        /// <summary>首个失配帧号；全部匹配时为 -1。</summary>
        public int MismatchFrameIndex { get; }

        /// <summary>期望校验和。</summary>
        public long ExpectedChecksum { get; }

        /// <summary>实际校验和。</summary>
        public long ActualChecksum { get; }

        BattleReplayVerifyResult(
            bool matched,
            int comparedFrames,
            int mismatchFrameIndex,
            long expectedChecksum,
            long actualChecksum)
        {
            Matched = matched;
            ComparedFrames = comparedFrames;
            MismatchFrameIndex = mismatchFrameIndex;
            ExpectedChecksum = expectedChecksum;
            ActualChecksum = actualChecksum;
        }

        /// <summary>创建全部匹配的结果。</summary>
        /// <param name="comparedFrames">比较帧数。</param>
        /// <returns>匹配结果。</returns>
        public static BattleReplayVerifyResult Ok(int comparedFrames) =>
            new BattleReplayVerifyResult(true, comparedFrames, -1, 0, 0);

        /// <summary>创建失配结果。</summary>
        /// <param name="comparedFrames">已比较帧数（含失配帧）。</param>
        /// <param name="mismatchFrameIndex">失配帧号。</param>
        /// <param name="expectedChecksum">录像中的校验和。</param>
        /// <param name="actualChecksum">重放得到的校验和。</param>
        /// <returns>失配结果。</returns>
        public static BattleReplayVerifyResult Fail(
            int comparedFrames,
            int mismatchFrameIndex,
            long expectedChecksum,
            long actualChecksum) =>
            new BattleReplayVerifyResult(
                false,
                comparedFrames,
                mismatchFrameIndex,
                expectedChecksum,
                actualChecksum);
    }

    /// <summary>
    /// 用录像指令驱动影子 Session 重放，不跑 AI 收集，只 Apply 已录命令。
    /// </summary>
    public static class BattleReplayVerifier
    {
        /// <summary>
        /// 在已创建好相同 Actor 的 Session 上重放录像，逐帧比较校验和。
        /// </summary>
        /// <param name="tape">录像带；不可为 null。</param>
        /// <param name="session">影子会话，须用相同种子并完成 <c>CreateActors</c>；不可为 null。</param>
        /// <param name="afterFixedStep">每逻辑步模拟后的回调（如刷波）；可为 null。</param>
        /// <returns>对拍结果。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="tape"/> 或 <paramref name="session"/> 为 null。</exception>
        public static BattleReplayVerifyResult Run(
            BattleReplayTape tape,
            BattleSession session,
            Action<float> afterFixedStep = null)
        {
            if (tape == null)
            {
                throw new ArgumentNullException(nameof(tape));
            }

            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            session.FixedDeltaTime = tape.FixedDeltaTime;
            var frames = tape.Frames;
            for (var i = 0; i < frames.Count; i++)
            {
                var recorded = frames[i];
                session.Tick(
                    tape.FixedDeltaTime,
                    _ => BattleIntentApplier.ApplyAll(session.Framework, recorded.Commands),
                    afterFixedStep);

                var actual = BattleFrameChecksum.Compute(session.Framework);
                if (actual != recorded.Checksum)
                {
                    return BattleReplayVerifyResult.Fail(i + 1, recorded.FrameIndex, recorded.Checksum, actual);
                }
            }

            return BattleReplayVerifyResult.Ok(frames.Count);
        }
    }
}
