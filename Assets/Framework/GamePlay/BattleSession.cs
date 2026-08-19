using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Events;
using Framework.Logging;
using Framework.Res;
using Framework.FixedMath;
using UnityEngine;

namespace Framework.GamePlay
{
    /// <summary>
    /// 一场战斗的独立会话：持有独立 <see cref="GamePlayFramework"/>、<see cref="ResourceScope"/> 与 ActorId 分配器。
    /// 由 <see cref="BattleDirector"/> 创建与销毁，业务层通过其引用驱动表现。
    /// </summary>
    public sealed class BattleSession : IDisposable
    {
        uint _nextActorId = 1;
        bool _disposed;
        float _tickAccumulator;

        /// <summary>会话唯一标识，由 <see cref="BattleDirector"/> 分配。</summary>
        public int SessionId { get; }

        /// <summary>当前会话的玩法框架实例（独立 World / Registry / EventBus）。</summary>
        public GamePlayFramework Framework { get; }

        /// <summary>会话级资源作用域；随 <see cref="Dispose"/> 统一释放。</summary>
        public ResourceScope Scope { get; }

        /// <summary>表现层事件总线的快捷访问。</summary>
        public IEventBus EventBus => Framework.EventBus;

        /// <summary>会话是否已释放。</summary>
        public bool IsDisposed => _disposed;

        /// <summary>会话是否正在运行（未暂停、未释放）。</summary>
        public bool IsRunning { get; set; } = true;

        /// <summary>逻辑固定步长（秒，定点）。</summary>
        public FP FixedDeltaTime { get; set; } = (FP)(1) / 30;

        /// <summary>当前渲染帧的逻辑插值系数，范围 [0, 1]。</summary>
        public float InterpolationAlpha => FixedDeltaTime > FP.Zero
            ? Mathf.Clamp01(_tickAccumulator / FixedDeltaTime.AsFloat())
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
            Scope = ResourceScope.Create($"BattleSession_{sessionId}");
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

        /// <summary>按固定步长推进战斗逻辑，并在每个逻辑步前后执行回调。</summary>
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

            if (FixedDeltaTime <= FP.Zero)
            {
                throw new InvalidOperationException("BattleSession.FixedDeltaTime must be greater than zero.");
            }

            var stepSeconds = FixedDeltaTime.AsFloat();
            _tickAccumulator = Mathf.Min(_tickAccumulator + Mathf.Max(0f, deltaTime), stepSeconds * 4f);
            var stepped = 0;
            while (_tickAccumulator >= stepSeconds)
            {
                beforeFixedStep?.Invoke(FixedDeltaTime);
                Framework.Tick(FixedDeltaTime);
                afterFixedStep?.Invoke(FixedDeltaTime);
                _tickAccumulator -= stepSeconds;
                stepped++;
            }

            return stepped;
        }

        /// <summary>释放会话：销毁 Framework、释放 ResourceScope。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            IsRunning = false;
            _tickAccumulator = 0f;
            Framework.Dispose();
            Scope.Dispose();
            GameLog.Info(LogCategories.GamePlay, $"Session {LogStyle.Value(SessionId.ToString())} disposed");
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
