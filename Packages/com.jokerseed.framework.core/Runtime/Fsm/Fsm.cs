using System;
using System.Collections.Generic;

namespace Framework.Fsm
{
    /// <summary>
    /// 有限状态机：按类型注册状态，同一时刻只有一个当前状态。
    /// 不依赖 Unity；由持有者在自己的更新循环里调用 <see cref="Update"/>。
    /// </summary>
    /// <typeparam name="TOwner">状态机持有者类型。</typeparam>
    public sealed class Fsm<TOwner> where TOwner : class
    {
        readonly Dictionary<Type, FsmState<TOwner>> _states = new Dictionary<Type, FsmState<TOwner>>();
        readonly Dictionary<string, object> _data = new Dictionary<string, object>();
        bool _exiting;
        bool _destroyed;

        /// <summary>创建状态机并对每个状态调用 <see cref="FsmState{TOwner}.OnInit"/>。</summary>
        /// <param name="name">状态机名称，用于日志与调试。</param>
        /// <param name="owner">持有者，不可为 null。</param>
        /// <param name="states">全部状态；同一类型只能出现一次。</param>
        /// <exception cref="ArgumentNullException"><paramref name="owner"/> 或 <paramref name="states"/> 为 null，或其中有 null。</exception>
        /// <exception cref="ArgumentException">状态为空或同一类型重复。</exception>
        public Fsm(string name, TOwner owner, params FsmState<TOwner>[] states)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Name = name ?? string.Empty;
            if (states == null)
            {
                throw new ArgumentNullException(nameof(states));
            }

            if (states.Length == 0)
            {
                throw new ArgumentException("FSM requires at least one state.", nameof(states));
            }

            for (var i = 0; i < states.Length; i++)
            {
                var state = states[i] ?? throw new ArgumentNullException(nameof(states), $"State at index {i} is null.");
                var type = state.GetType();
                if (_states.ContainsKey(type))
                {
                    throw new ArgumentException($"FSM '{Name}' already contains state {type.Name}.", nameof(states));
                }

                _states.Add(type, state);
            }

            for (var i = 0; i < states.Length; i++)
            {
                states[i].InvokeInit(this);
            }
        }

        /// <summary>状态机名称。</summary>
        public string Name { get; }

        /// <summary>持有者。</summary>
        public TOwner Owner { get; }

        /// <summary>当前状态；未启动时为 null。</summary>
        public FsmState<TOwner> CurrentState { get; private set; }

        /// <summary>进入当前状态后累计的 <see cref="Update"/> 时间（秒）。</summary>
        public float CurrentStateTime { get; private set; }

        /// <summary>是否已启动且未销毁。</summary>
        public bool IsRunning => CurrentState != null && !_destroyed;

        /// <summary>状态切换后触发，参数为（旧状态，新状态）；首次启动时旧状态为 null。</summary>
        public event Action<FsmState<TOwner>, FsmState<TOwner>> StateChanged;

        /// <summary>以指定状态启动。</summary>
        /// <typeparam name="TState">初始状态类型。</typeparam>
        /// <exception cref="InvalidOperationException">已启动、已销毁或状态未注册。</exception>
        public void Start<TState>() where TState : FsmState<TOwner> => Start(typeof(TState));

        /// <summary>以指定状态启动。</summary>
        /// <param name="stateType">初始状态类型。</param>
        /// <exception cref="InvalidOperationException">已启动、已销毁或状态未注册。</exception>
        public void Start(Type stateType)
        {
            EnsureAlive();
            if (CurrentState != null)
            {
                throw new InvalidOperationException($"FSM '{Name}' is already running.");
            }

            EnterState(GetStateOrThrow(stateType), null);
        }

        /// <summary>切换到指定类型的状态：先调用当前状态的 OnExit，再调用新状态的 OnEnter。</summary>
        /// <typeparam name="TState">目标状态类型。</typeparam>
        /// <exception cref="InvalidOperationException">未启动、已销毁、状态未注册，或在 OnExit 中调用。</exception>
        public void ChangeState<TState>() where TState : FsmState<TOwner> => ChangeState(typeof(TState));

        /// <summary>切换到指定类型的状态：先调用当前状态的 OnExit，再调用新状态的 OnEnter。</summary>
        /// <param name="stateType">目标状态类型。</param>
        /// <exception cref="InvalidOperationException">未启动、已销毁、状态未注册，或在 OnExit 中调用。</exception>
        public void ChangeState(Type stateType)
        {
            EnsureAlive();
            if (CurrentState == null)
            {
                throw new InvalidOperationException($"FSM '{Name}' is not started. Call Start first.");
            }

            if (_exiting)
            {
                throw new InvalidOperationException($"FSM '{Name}' cannot change state inside OnExit.");
            }

            var next = GetStateOrThrow(stateType);
            var previous = CurrentState;
            _exiting = true;
            try
            {
                previous.InvokeExit(this);
            }
            finally
            {
                _exiting = false;
            }

            EnterState(next, previous);
        }

        /// <summary>驱动当前状态的 OnUpdate。</summary>
        /// <param name="deltaTime">距上次更新的时间（秒）。</param>
        public void Update(float deltaTime)
        {
            if (!IsRunning)
            {
                return;
            }

            CurrentStateTime += deltaTime;
            CurrentState.InvokeUpdate(this, deltaTime);
        }

        /// <summary>是否注册了指定类型的状态。</summary>
        /// <typeparam name="TState">状态类型。</typeparam>
        /// <returns>已注册返回 true。</returns>
        public bool HasState<TState>() where TState : FsmState<TOwner> => _states.ContainsKey(typeof(TState));

        /// <summary>获取已注册的状态实例。</summary>
        /// <typeparam name="TState">状态类型。</typeparam>
        /// <returns>状态实例。</returns>
        /// <exception cref="InvalidOperationException">状态未注册。</exception>
        public TState GetState<TState>() where TState : FsmState<TOwner> => (TState)GetStateOrThrow(typeof(TState));

        /// <summary>写入状态间共享数据。</summary>
        /// <param name="key">键。</param>
        /// <param name="value">值。</param>
        public void SetData(string key, object value) => _data[key] = value;

        /// <summary>读取状态间共享数据。</summary>
        /// <typeparam name="T">值类型。</typeparam>
        /// <param name="key">键。</param>
        /// <param name="value">存在且类型匹配时输出值。</param>
        /// <returns>存在且类型匹配返回 true。</returns>
        public bool TryGetData<T>(string key, out T value)
        {
            if (_data.TryGetValue(key, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>删除共享数据。</summary>
        /// <param name="key">键。</param>
        /// <returns>存在并删除返回 true。</returns>
        public bool RemoveData(string key) => _data.Remove(key);

        /// <summary>退出当前状态并对所有状态调用 OnDestroy；之后状态机不可再用。</summary>
        public void Destroy()
        {
            if (_destroyed)
            {
                return;
            }

            if (CurrentState != null)
            {
                _exiting = true;
                try
                {
                    CurrentState.InvokeExit(this);
                }
                finally
                {
                    _exiting = false;
                }
            }

            _destroyed = true;
            CurrentState = null;
            foreach (var state in _states.Values)
            {
                state.InvokeDestroy(this);
            }

            _data.Clear();
            StateChanged = null;
        }

        void EnterState(FsmState<TOwner> next, FsmState<TOwner> previous)
        {
            CurrentState = next;
            CurrentStateTime = 0f;
            StateChanged?.Invoke(previous, next);
            next.InvokeEnter(this);
        }

        FsmState<TOwner> GetStateOrThrow(Type stateType)
        {
            if (stateType == null || !_states.TryGetValue(stateType, out var state))
            {
                throw new InvalidOperationException($"FSM '{Name}' has no state {stateType?.Name ?? "null"}.");
            }

            return state;
        }

        void EnsureAlive()
        {
            if (_destroyed)
            {
                throw new InvalidOperationException($"FSM '{Name}' is destroyed.");
            }
        }
    }
}
