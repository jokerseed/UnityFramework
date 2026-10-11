using System;
using System.Collections.Generic;

namespace Framework.Lockstep
{
    /// <summary>一帧里某个玩家发来的输入。</summary>
    public sealed class LockstepInputMessage
    {
        /// <summary>逻辑步，从 0 起。</summary>
        public int Tick { get; }

        /// <summary>玩家 ID。</summary>
        public int PlayerId { get; }

        /// <summary>输入载荷。主机保存的是副本。</summary>
        public byte[] Payload { get; }

        /// <summary>构造一帧输入。</summary>
        /// <param name="tick">逻辑步。</param>
        /// <param name="playerId">玩家 ID。</param>
        /// <param name="payload">载荷；null 当作空数组。</param>
        public LockstepInputMessage(int tick, int playerId, byte[] payload)
        {
            Tick = tick;
            PlayerId = playerId;
            Payload = payload ?? Array.Empty<byte>();
        }
    }

    /// <summary>
    /// 等输入锁步，外加按检查点回滚。
    /// 某一步在所有玩家的输入到齐之前不会调用 <see cref="ILockstepSimulation.Simulate"/>。
    /// 已经模拟过的步若收到不同输入，会恢复不大于该步的检查点并重放。
    /// 真实传输不在这里；测试使用 <see cref="LoopbackCommunicator"/>。
    /// </summary>
    public sealed class LockstepHost
    {
        /// <summary>输入事件码。其他事件码会被忽略。</summary>
        public const byte InputEventCode = 42;

        readonly ICommunicator _communicator;
        readonly ILockstepSimulation _simulation;
        readonly int _localPlayerId;
        readonly int _localIndex;
        readonly int _interval;
        readonly int[] _playerIds;
        readonly List<byte[][]> _inputs = new List<byte[][]>();
        readonly List<bool[]> _predicted = new List<bool[]>();
        readonly List<long> _checksums = new List<long>();

        /// <summary>创建主机并监听通讯端上的输入事件。</summary>
        /// <param name="communicator">通讯端；不可为 null。</param>
        /// <param name="localPlayerId">本端玩家，必须出现在 <paramref name="playerIds"/> 里。</param>
        /// <param name="playerIds">本场玩家。两端必须传同一组 ID，内部会排序。</param>
        /// <param name="snapshotInterval">每隔多少步保存一次检查点，至少为 1。0 号步总会保存。</param>
        /// <param name="simulation">模拟；不可为 null。</param>
        /// <exception cref="ArgumentNullException">通讯端、玩家列表或模拟为 null。</exception>
        /// <exception cref="ArgumentException">间隔小于 1，或本端不在玩家列表中，或玩家 ID 重复。</exception>
        public LockstepHost(
            ICommunicator communicator,
            int localPlayerId,
            int[] playerIds,
            int snapshotInterval,
            ILockstepSimulation simulation)
        {
            if (communicator == null)
            {
                throw new ArgumentNullException(nameof(communicator));
            }

            if (playerIds == null)
            {
                throw new ArgumentNullException(nameof(playerIds));
            }

            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (snapshotInterval < 1)
            {
                throw new ArgumentException("Snapshot interval must be >= 1.", nameof(snapshotInterval));
            }

            _communicator = communicator;
            _simulation = simulation;
            _localPlayerId = localPlayerId;
            _interval = snapshotInterval;
            _playerIds = new int[playerIds.Length];
            Array.Copy(playerIds, _playerIds, playerIds.Length);
            Array.Sort(_playerIds);
            _localIndex = IndexOf(localPlayerId);
            if (_localIndex < 0)
            {
                throw new ArgumentException("Local player is not in the match.", nameof(localPlayerId));
            }

            for (var i = 1; i < _playerIds.Length; i++)
            {
                if (_playerIds[i] == _playerIds[i - 1])
                {
                    throw new ArgumentException("Duplicate player id " + _playerIds[i], nameof(playerIds));
                }
            }

            _communicator.AddEventListener(OnEvent);
        }

        /// <summary>下一帧尚未模拟的逻辑步。</summary>
        public int ConfirmedTick { get; private set; }

        /// <summary>已经模拟的每一步校验和，下标就是逻辑步。</summary>
        public IReadOnlyList<long> Checksums => _checksums;

        /// <summary>提交本端这一步的输入，并广播给其他玩家。</summary>
        /// <param name="tick">逻辑步。</param>
        /// <param name="payload">载荷；null 当作空数组。</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="tick"/> 小于 0。</exception>
        public void SubmitLocal(int tick, byte[] payload)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick));
            }

            var copy = Copy(payload);
            Store(_localIndex, tick, copy, predicted: false);
            _communicator.OpRaiseEvent(
                InputEventCode,
                new LockstepInputMessage(tick, _localPlayerId, copy),
                true,
                null);
        }

        /// <summary>
        /// 给当前尚未模拟的步填一个远端占位输入。
        /// 真正的输入稍后到达且内容不同时，主机会回滚重放。
        /// </summary>
        /// <param name="playerId">远端玩家。</param>
        /// <param name="payload">占位载荷。</param>
        /// <returns>写入了占位时返回 true。该玩家这一步已有正式输入时返回 false。</returns>
        /// <exception cref="ArgumentException"><paramref name="playerId"/> 不是本场玩家，或就是本端。</exception>
        public bool PredictRemote(int playerId, byte[] payload)
        {
            var index = IndexOf(playerId);
            if (index < 0)
            {
                throw new ArgumentException("Unknown player " + playerId, nameof(playerId));
            }

            if (index == _localIndex)
            {
                throw new ArgumentException("Local input is submitted, not predicted.", nameof(playerId));
            }

            EnsureTick(ConfirmedTick);
            if (_inputs[ConfirmedTick][index] != null && !_predicted[ConfirmedTick][index])
            {
                return false;
            }

            Store(index, ConfirmedTick, Copy(payload), predicted: true);
            return true;
        }

        /// <summary>若当前步所有玩家的输入都已到齐，则模拟一帧。</summary>
        /// <returns>模拟了一帧时返回 true。</returns>
        public bool TryAdvance()
        {
            if (!HasAll(ConfirmedTick))
            {
                return false;
            }

            AdvanceOne();
            return true;
        }

        void OnEvent(byte eventCode, object content)
        {
            if (eventCode != InputEventCode)
            {
                return;
            }

            var message = content as LockstepInputMessage;
            if (message == null || message.PlayerId == _localPlayerId)
            {
                return;
            }

            var index = IndexOf(message.PlayerId);
            if (index < 0 || message.Tick < 0)
            {
                return;
            }

            Store(index, message.Tick, Copy(message.Payload), predicted: false);
        }

        void AdvanceOne()
        {
            if (ConfirmedTick % _interval == 0)
            {
                _simulation.SaveCheckpoint(ConfirmedTick);
            }

            _simulation.Simulate(ConfirmedTick, _playerIds, _inputs[ConfirmedTick]);
            _checksums.Add(_simulation.Checksum);
            ConfirmedTick++;
        }

        void Store(int playerIndex, int tick, byte[] payload, bool predicted)
        {
            EnsureTick(tick);
            var previous = _inputs[tick][playerIndex];
            var changed = !Same(previous, payload);
            _inputs[tick][playerIndex] = payload;
            _predicted[tick][playerIndex] = predicted;
            if (changed && tick < ConfirmedTick)
            {
                RollbackFrom(tick);
            }
        }

        void RollbackFrom(int tick)
        {
            var snap = _simulation.LatestCheckpointAtOrBefore(tick);
            var target = ConfirmedTick;
            _simulation.RestoreCheckpoint(snap);
            ConfirmedTick = snap;
            if (snap < _checksums.Count)
            {
                _checksums.RemoveRange(snap, _checksums.Count - snap);
            }

            while (ConfirmedTick < target)
            {
                AdvanceOne();
            }
        }

        void EnsureTick(int tick)
        {
            while (_inputs.Count <= tick)
            {
                _inputs.Add(new byte[_playerIds.Length][]);
                _predicted.Add(new bool[_playerIds.Length]);
            }
        }

        bool HasAll(int tick)
        {
            if (tick >= _inputs.Count)
            {
                return false;
            }

            var row = _inputs[tick];
            for (var i = 0; i < row.Length; i++)
            {
                if (row[i] == null)
                {
                    return false;
                }
            }

            return true;
        }

        int IndexOf(int playerId)
        {
            for (var i = 0; i < _playerIds.Length; i++)
            {
                if (_playerIds[i] == playerId)
                {
                    return i;
                }
            }

            return -1;
        }

        static byte[] Copy(byte[] payload)
        {
            if (payload == null || payload.Length == 0)
            {
                return Array.Empty<byte>();
            }

            var copy = new byte[payload.Length];
            Array.Copy(payload, copy, payload.Length);
            return copy;
        }

        static bool Same(byte[] left, byte[] right)
        {
            if (left == null || right == null)
            {
                return left == right;
            }

            if (left.Length != right.Length)
            {
                return false;
            }

            for (var i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
