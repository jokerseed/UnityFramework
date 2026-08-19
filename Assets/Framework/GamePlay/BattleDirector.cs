using System;
using System.Collections.Generic;
using Framework.Logging;

namespace Framework.GamePlay
{
    /// <summary>
    /// 战斗会话管理器：Session 工厂 + 集中 Tick。
    /// 由 <see cref="GamePlayModule"/> 创建并持有，外部通过 <see cref="GamePlayModule.Director"/> 访问。
    /// </summary>
    public sealed class BattleDirector : IDisposable
    {
        readonly Dictionary<int, BattleSession> _sessions = new Dictionary<int, BattleSession>(4);
        int _nextSessionId = 1;

        /// <summary>当前活跃 Session 数量。</summary>
        public int SessionCount => _sessions.Count;

        /// <summary>创建并返回一个新的 <see cref="BattleSession"/>。</summary>
        /// <param name="randomSeed">本场战斗确定性随机种子。</param>
        /// <returns>已初始化的会话实例。</returns>
        public BattleSession CreateSession(int randomSeed = 1)
        {
            var id = _nextSessionId++;
            var session = new BattleSession(id, randomSeed);
            _sessions[id] = session;
            GameLog.Info(
                LogCategories.GamePlay,
                $"Session {LogStyle.Value(id.ToString())} created  seed={LogStyle.Value(randomSeed.ToString())}");
            return session;
        }

        /// <summary>获取指定 ID 的 Session；不存在时返回 null。</summary>
        /// <param name="sessionId">会话 ID。</param>
        /// <returns>会话实例或 null。</returns>
        public BattleSession GetSession(int sessionId)
        {
            _sessions.TryGetValue(sessionId, out var session);
            return session;
        }

        /// <summary>销毁并移除指定 Session。</summary>
        /// <param name="sessionId">会话 ID。</param>
        /// <returns>成功销毁时返回 true；Session 不存在或已销毁时返回 false。</returns>
        public bool DestroySession(int sessionId)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                return false;
            }

            _sessions.Remove(sessionId);
            session.Dispose();
            return true;
        }

        /// <summary>销毁指定 Session 实例。</summary>
        /// <param name="session">要销毁的会话；不可为 null。</param>
        /// <returns>成功销毁时返回 true。</returns>
        public bool DestroySession(BattleSession session)
        {
            if (session == null)
            {
                return false;
            }

            return DestroySession(session.SessionId);
        }

        /// <summary>集中驱动所有活跃 Session 的战斗逻辑。</summary>
        /// <param name="deltaTime">帧间隔（秒）。</param>
        public void Tick(float deltaTime)
        {
            foreach (var pair in _sessions)
            {
                pair.Value.Tick(deltaTime);
            }
        }

        /// <summary>销毁全部 Session 并重置状态。</summary>
        public void Dispose()
        {
            foreach (var pair in _sessions)
            {
                pair.Value.Dispose();
            }

            _sessions.Clear();
            GameLog.Info(LogCategories.GamePlay, "BattleDirector disposed all sessions");
        }
    }
}
