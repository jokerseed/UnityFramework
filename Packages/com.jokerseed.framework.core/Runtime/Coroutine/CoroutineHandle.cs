namespace Framework.Coroutine
{
    /// <summary>
    /// 由 <see cref="CoroutineManager"/> 返回的协程句柄。
    /// 协程正常结束、抛出异常、被 <see cref="Stop"/> / 宿主 <c>HaltAll</c> 停止，或宿主失活、销毁后，<see cref="IsRunning"/> 均为 false。
    /// </summary>
    sealed class CoroutineHandle : ICoroutineHandle
    {
        CoroutineRunnerBase _runner;
        UnityEngine.Coroutine _coroutine;
        bool _running;

        /// <summary>表示启动失败的无效句柄。</summary>
        public static CoroutineHandle Invalid { get; } = new CoroutineHandle();

        CoroutineHandle()
        {
            _running = false;
        }

        internal static CoroutineHandle CreatePending()
        {
            return new CoroutineHandle { _running = true };
        }

        internal void Bind(CoroutineRunnerBase runner, UnityEngine.Coroutine coroutine)
        {
            if (!_running)
            {
                // 协程在 StartCoroutine 的第一步里就已结束。
                return;
            }

            if (runner == null || coroutine == null)
            {
                _running = false;
                return;
            }

            _runner = runner;
            _coroutine = coroutine;
            runner.Track(this);
        }

        /// <inheritdoc />
        public bool IsRunning => _running && _runner != null && _coroutine != null;

        /// <inheritdoc />
        public void Stop()
        {
            if (!_running)
            {
                return;
            }

            var runner = _runner;
            var coroutine = _coroutine;
            MarkStopped();
            if (runner != null && coroutine != null)
            {
                runner.StopCoroutine(coroutine);
            }
        }

        /// <inheritdoc />
        public void Dispose() => Stop();

        /// <summary>协程正常结束或因异常结束。</summary>
        internal void MarkCompleted() => MarkStopped();

        /// <summary>协程已不再运行（任何原因）；从宿主登记中移除。</summary>
        internal void MarkStopped()
        {
            _running = false;
            var runner = _runner;
            _coroutine = null;
            _runner = null;
            if (runner != null)
            {
                runner.Untrack(this);
            }
        }
    }
}
