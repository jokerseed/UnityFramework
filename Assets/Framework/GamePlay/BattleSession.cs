using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Events;
using Framework.Logging;
using Framework.Res;
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

        internal BattleSession(int sessionId)
        {
            SessionId = sessionId;
            Framework = new GamePlayFramework();
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

        /// <summary>推进一帧战斗逻辑。</summary>
        /// <param name="deltaTime">帧间隔（秒）。</param>
        public void Tick(float deltaTime)
        {
            if (_disposed || !IsRunning)
            {
                return;
            }

            Framework.Tick(deltaTime);
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
