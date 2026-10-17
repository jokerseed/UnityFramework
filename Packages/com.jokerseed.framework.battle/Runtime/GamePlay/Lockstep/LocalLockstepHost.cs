using System;
using Framework.FixedMath;

namespace Framework.GamePlay
{
    /// <summary>
    /// 单机锁步 Host：本机编码的意图帧入队后立刻出队执行。
    /// 逻辑只使用传入的 unscaled 时间推进固定步长，不受 <c>Time.timeScale</c> 影响。
    /// 校验和要遍历整个世界并排序，默认不算：<see cref="RecordReplay"/> 为 true 时每步计算并写入内存录像，
    /// 否则只在 <see cref="ChecksumInterval"/> 大于 0 时每 N 步计算一次。
    /// </summary>
    public sealed class LocalLockstepHost
    {
        readonly BattleSession _session;
        readonly BattleIntentFrameQueue _queue = new BattleIntentFrameQueue();
        readonly BattleReplayRecorder _recorder = new BattleReplayRecorder();
        int _nextFrameIndex;
        BattleIntentFrame _pendingRecord;

        /// <summary>使用已创建的战斗会话构造 Host。</summary>
        /// <param name="session">战斗会话；不可为 null。</param>
        public LocalLockstepHost(BattleSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        /// <summary>意图帧队列。</summary>
        public BattleIntentFrameQueue Queue => _queue;

        /// <summary>内存录像器。</summary>
        public BattleReplayRecorder Recorder => _recorder;

        /// <summary>
        /// 输入在队列里多留几帧再执行。0 与原来一样，入队后立刻出队，已有录像不变。
        /// </summary>
        public int InputDelayFrames { get; set; }

        /// <summary>下一逻辑帧号。</summary>
        public int NextFrameIndex => _nextFrameIndex;

        /// <summary>最近一次计算的校验和；从未计算时为 0。</summary>
        public long LastChecksum { get; private set; }

        /// <summary>是否录制内存录像（每步计算校验和并保存意图帧）。默认 false；调试或需要对拍时打开。</summary>
        public bool RecordReplay { get; set; }

        /// <summary>不录像时每隔多少逻辑步计算一次校验和；小于等于 0 表示不计算。默认 0。</summary>
        public int ChecksumInterval { get; set; }

        /// <summary>当前渲染插值系数，转发自 Session。</summary>
        public float InterpolationAlpha => _session.InterpolationAlpha;

        /// <summary>
        /// 按 unscaled 时间追赶逻辑帧。每步：玩家编码 → AI 收集 → 入队 → 出队 → 执行 → 模拟 → 刷波 → 校验录像。
        /// </summary>
        /// <param name="unscaledDeltaTime">不受 timeScale 影响的时间增量。</param>
        /// <param name="fillFrame">把本机采样编码进该帧；不可为 null。</param>
        /// <param name="afterFixedStep">每个逻辑步模拟之后、校验之前的回调（如刷波）。</param>
        /// <returns>本次推进的逻辑步数。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="fillFrame"/> 为 null。</exception>
        public int Tick(
            float unscaledDeltaTime,
            Action<BattleIntentFrame> fillFrame,
            Action<FP> afterFixedStep = null)
        {
            if (fillFrame == null)
            {
                throw new ArgumentNullException(nameof(fillFrame));
            }

            return _session.Tick(unscaledDeltaTime, _ =>
            {
                var frame = _queue.Rent();
                frame.FrameIndex = _nextFrameIndex++;
                fillFrame(frame);
                _session.Framework.CollectAiIntents(frame.Commands, _session.FixedDeltaTime);
                _queue.Enqueue(frame);

                while (_queue.Count > InputDelayFrames && _queue.TryDequeue(out var ready))
                {
                    BattleIntentApplier.ApplyAll(_session.Framework, ready.Commands);
                    _pendingRecord = ready;
                }
            }, dt =>
            {
                afterFixedStep?.Invoke(dt);
                var record = RecordReplay && _pendingRecord != null;
                if (record || (ChecksumInterval > 0 && (_nextFrameIndex - 1) % ChecksumInterval == 0))
                {
                    LastChecksum = BattleFrameChecksum.Compute(_session.Framework);
                }

                if (_pendingRecord != null)
                {
                    if (record)
                    {
                        _recorder.Capture(_pendingRecord, LastChecksum);
                    }

                    _queue.Recycle(_pendingRecord);
                    _pendingRecord = null;
                }
            });
        }

        /// <summary>清空未执行帧与录像。</summary>
        public void Clear()
        {
            if (_pendingRecord != null)
            {
                _queue.Recycle(_pendingRecord);
                _pendingRecord = null;
            }

            _queue.Clear();
            _recorder.Clear();
            LastChecksum = 0;
        }
    }
}
