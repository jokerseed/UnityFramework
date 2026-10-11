using System;
using System.Collections.Generic;

namespace Framework.Lockstep
{
    /// <summary>进程内的 <see cref="ICommunicator"/> 枢纽。测试用它把多个对端接在一起，不走真实网络。</summary>
    public sealed class LoopbackHub
    {
        readonly Dictionary<int, LoopbackCommunicator> _peers = new Dictionary<int, LoopbackCommunicator>();

        /// <summary>打开一个对端。</summary>
        /// <param name="playerId">玩家 ID；同一枢纽内不可重复。</param>
        /// <returns>该玩家的通讯端。</returns>
        /// <exception cref="ArgumentException">玩家 ID 已存在。</exception>
        public LoopbackCommunicator Open(int playerId)
        {
            if (_peers.ContainsKey(playerId))
            {
                throw new ArgumentException("Duplicate loopback player " + playerId, nameof(playerId));
            }

            var peer = new LoopbackCommunicator(this, playerId);
            _peers[playerId] = peer;
            return peer;
        }

        internal void Deliver(int fromPlayer, byte eventCode, object message, bool reliable, int[] toPlayers)
        {
            foreach (var pair in _peers)
            {
                if (pair.Key == fromPlayer)
                {
                    continue;
                }

                if (!Addresses(toPlayers, pair.Key))
                {
                    continue;
                }

                pair.Value.Receive(eventCode, message, reliable);
            }
        }

        static bool Addresses(int[] toPlayers, int playerId)
        {
            if (toPlayers == null)
            {
                return true;
            }

            for (var i = 0; i < toPlayers.Length; i++)
            {
                if (toPlayers[i] == playerId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 内存环回通讯。默认立刻把事件交给其他对端。
    /// <see cref="HoldOutgoing"/> 为 true 时先排队，<see cref="Flush"/> 再按提交顺序送出，用来模拟迟到输入。
    /// </summary>
    public sealed class LoopbackCommunicator : ICommunicator
    {
        readonly LoopbackHub _hub;
        readonly int _playerId;
        readonly List<OnEventReceived> _listeners = new List<OnEventReceived>();
        readonly List<Pending> _held = new List<Pending>();

        internal LoopbackCommunicator(LoopbackHub hub, int playerId)
        {
            _hub = hub;
            _playerId = playerId;
        }

        /// <summary>为 true 时 <see cref="OpRaiseEvent"/> 不立刻投递。</summary>
        public bool HoldOutgoing { get; set; }

        /// <summary>本端玩家 ID。</summary>
        public int PlayerId => _playerId;

        /// <inheritdoc />
        public int RoundTripTime() => 0;

        /// <inheritdoc />
        public void OpRaiseEvent(byte eventCode, object message, bool reliable, int[] toPlayers)
        {
            if (HoldOutgoing)
            {
                _held.Add(new Pending(eventCode, message, reliable, toPlayers));
                return;
            }

            _hub.Deliver(_playerId, eventCode, message, reliable, toPlayers);
        }

        /// <summary>把暂存的出向事件按顺序交给其他对端。</summary>
        public void Flush()
        {
            for (var i = 0; i < _held.Count; i++)
            {
                var item = _held[i];
                _hub.Deliver(_playerId, item.EventCode, item.Message, item.Reliable, item.ToPlayers);
            }

            _held.Clear();
        }

        /// <inheritdoc />
        public void AddEventListener(OnEventReceived onEventReceived)
        {
            if (onEventReceived == null)
            {
                throw new ArgumentNullException(nameof(onEventReceived));
            }

            _listeners.Add(onEventReceived);
        }

        internal void Receive(byte eventCode, object message, bool reliable)
        {
            for (var i = 0; i < _listeners.Count; i++)
            {
                _listeners[i](eventCode, message);
            }
        }

        readonly struct Pending
        {
            public readonly byte EventCode;
            public readonly object Message;
            public readonly bool Reliable;
            public readonly int[] ToPlayers;

            public Pending(byte eventCode, object message, bool reliable, int[] toPlayers)
            {
                EventCode = eventCode;
                Message = message;
                Reliable = reliable;
                ToPlayers = toPlayers;
            }
        }
    }
}
