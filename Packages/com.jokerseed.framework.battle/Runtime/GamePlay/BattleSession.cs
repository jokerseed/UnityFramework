using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Events;
using Framework.Logging;
using Framework.FixedMath;

namespace Framework.GamePlay
{
    /// <summary>
    /// 一场战斗的独立会话：持有独立 <see cref="GamePlayFramework"/>、ActorId 分配器和随会话释放的资源。
    /// 由 <see cref="BattleDirector"/> 创建与销毁，业务层通过其引用驱动表现。
    /// </summary>
    public sealed class BattleSession : IDisposable
    {
        readonly List<IDisposable> _ownedResources = new List<IDisposable>();
        uint _nextActorId = 1;
        bool _disposed;
        FP _tickAccumulator;

        /// <summary>会话唯一标识，由 <see cref="BattleDirector"/> 分配。</summary>
        public int SessionId { get; }

        /// <summary>当前会话的玩法框架实例（独立 World / Registry / EventBus）。</summary>
        public GamePlayFramework Framework { get; }

        /// <summary>表现层事件总线的快捷访问。</summary>
        public IEventBus EventBus => Framework.EventBus;

        /// <summary>会话是否已释放。</summary>
        public bool IsDisposed => _disposed;

        /// <summary>会话是否正在运行（未暂停、未释放）。</summary>
        public bool IsRunning { get; set; } = true;

        /// <summary>逻辑固定步长（秒，定点）。</summary>
        public FP FixedDeltaTime { get; set; } = (FP)(1) / 30;

        /// <summary>
        /// 当前渲染帧的逻辑插值系数，范围 [0, 1]。
        /// 累加器本身是 <see cref="FP"/>；这里只为表现插值转成 float。
        /// </summary>
        public float InterpolationAlpha => FixedDeltaTime > FP.Zero
            ? TSMath.Clamp(_tickAccumulator / FixedDeltaTime, FP.Zero, FP.One).AsFloat()
            : 0f;

        /// <summary>本场战斗随机种子。</summary>
        public int RandomSeed { get; }

        /// <summary>本场战斗确定性随机源。</summary>
        public TSRandom Random => Framework.Random;

        internal BattleSession(int sessionId, int randomSeed = 1)
        {
            SessionId = sessionId;
            RandomSeed = randomSeed;
            Framework = new GamePlayFramework(randomSeed);
        }

        /// <summary>在本会话内分配一个唯一的 <see cref="ActorId"/>（从 1 自增）。</summary>
        /// <returns>新分配的 ActorId。</returns>
        public ActorId AllocateActorId()
        {
            ThrowIfDisposed();
            return new ActorId(_nextActorId++);
        }

        /// <summary>批量分配连续的 ActorId。</summary>
        /// <param name="count">数量，须大于 0。</param>
        /// <param name="results">输出列表；已有内容不会被清空。</param>
        public void AllocateActorIds(int count, List<ActorId> results)
        {
            ThrowIfDisposed();
            if (count <= 0)
            {
                return;
            }

            for (var i = 0; i < count; i++)
            {
                results.Add(new ActorId(_nextActorId++));
            }
        }

        /// <summary>
        /// 登记一个随会话一起释放的资源（例如表现层的资源作用域）。
        /// 会话 <see cref="Dispose"/> 时按登记的逆序释放。
        /// </summary>
        /// <param name="resource">要托管的资源；不可为 null。</param>
        /// <exception cref="ArgumentNullException"><paramref name="resource"/> 为 null。</exception>
        /// <exception cref="ObjectDisposedException">会话已释放。</exception>
        public void AttachResource(IDisposable resource)
        {
            ThrowIfDisposed();
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            _ownedResources.Add(resource);
        }

        /// <summary>
        /// 按渲染帧时间追赶逻辑步。传入的 float 只在入口转成 <see cref="FP"/>，
        /// 累加、比较和扣减都在定点上完成，避免 float 累加漂移导致步数不一致。
        /// 录像重放请用 <see cref="Step"/>，不要用 <c>FixedDeltaTime.AsFloat()</c> 反推一步。
        /// </summary>
        /// <param name="deltaTime">本次渲染帧累计的时间增量（秒）。</param>
        /// <param name="beforeFixedStep">每个逻辑步执行前的回调；参数为固定步长。</param>
        /// <param name="afterFixedStep">每个逻辑步执行后的回调；参数为固定步长。</param>
        /// <returns>本次渲染帧实际推进的逻辑步数。</returns>
        public int Tick(
            float deltaTime,
            Action<FP> beforeFixedStep = null,
            Action<FP> afterFixedStep = null)
        {
            if (_disposed || !IsRunning)
            {
                return 0;
            }

            EnsureFixedStep();
            var incoming = deltaTime > 0f ? (FP)deltaTime : FP.Zero;
            var maxAccum = FixedDeltaTime * 4;
            _tickAccumulator += incoming;
            if (_tickAccumulator > maxAccum)
            {
                _tickAccumulator = maxAccum;
            }

            var stepped = 0;
            while (_tickAccumulator >= FixedDeltaTime)
            {
                RunOneStep(beforeFixedStep, afterFixedStep);
                _tickAccumulator -= FixedDeltaTime;
                stepped++;
            }

            return stepped;
        }

        /// <summary>
        /// 推进恰好 <paramref name="stepCount"/> 个逻辑步，不经过 float 累加器。
        /// </summary>
        /// <param name="stepCount">逻辑步数；小于等于 0 时不推进。</param>
        /// <param name="beforeFixedStep">每个逻辑步执行前的回调；参数为固定步长。</param>
        /// <param name="afterFixedStep">每个逻辑步执行后的回调；参数为固定步长。</param>
        /// <returns>实际推进的逻辑步数。</returns>
        public int Step(
            int stepCount,
            Action<FP> beforeFixedStep = null,
            Action<FP> afterFixedStep = null)
        {
            if (_disposed || !IsRunning || stepCount <= 0)
            {
                return 0;
            }

            EnsureFixedStep();
            for (var i = 0; i < stepCount; i++)
            {
                RunOneStep(beforeFixedStep, afterFixedStep);
            }

            return stepCount;
        }

        /// <summary>释放会话：销毁 Framework，并按逆序释放 <see cref="AttachResource"/> 登记的资源。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            IsRunning = false;
            _tickAccumulator = FP.Zero;
            Framework.Dispose();
            for (var i = _ownedResources.Count - 1; i >= 0; i--)
            {
                try
                {
                    _ownedResources[i].Dispose();
                }
                catch (Exception ex)
                {
                    GameLog.Exception(LogCategories.GamePlay, ex, $"Session {SessionId} resource dispose failed");
                }
            }

            _ownedResources.Clear();
            GameLog.Info(LogCategories.GamePlay, $"Session {LogStyle.Value(SessionId.ToString())} disposed");
        }

        void EnsureFixedStep()
        {
            if (FixedDeltaTime <= FP.Zero)
            {
                throw new InvalidOperationException("BattleSession.FixedDeltaTime must be greater than zero.");
            }
        }

        void RunOneStep(Action<FP> beforeFixedStep, Action<FP> afterFixedStep)
        {
            beforeFixedStep?.Invoke(FixedDeltaTime);
            Framework.Tick(FixedDeltaTime);
            afterFixedStep?.Invoke(FixedDeltaTime);
        }

        void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(BattleSession), $"Session {SessionId}");
            }
        }
    }
}
