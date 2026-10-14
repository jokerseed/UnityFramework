using System;
using System.Collections.Generic;
using Framework.Logging;

namespace Framework.Events
{
    /// <summary>
    /// 单个事件类型的监听列表。
    /// 分发过程中的增删按调用顺序记入操作队列，分发结束后依次回放，
    /// 因此同一回调在分发中先 Remove 再 Add 的最终结果是"仍在列表中"。
    /// </summary>
    internal sealed class HandlerList<T> : IEventChannel where T : struct
    {
        struct PendingOp
        {
            public Action<T> Handler;
            public bool IsAdd;
        }

        readonly List<Action<T>> _handlers = new List<Action<T>>(4);
        readonly List<PendingOp> _pending = new List<PendingOp>(4);
        int _invokeDepth;

        public void Invoke(in T evt)
        {
            _invokeDepth++;
            try
            {
                for (var i = 0; i < _handlers.Count; i++)
                {
                    var handler = _handlers[i];
                    if (IsPendingRemove(handler))
                    {
                        continue;
                    }

                    try
                    {
                        handler(evt);
                    }
                    catch (Exception ex)
                    {
                        GameLog.Exception(LogCategories.Events, ex, "Event handler failed.");
                    }
                }
            }
            finally
            {
                _invokeDepth--;
                if (_invokeDepth == 0)
                {
                    ApplyPendingChanges();
                }
            }
        }

        public void Add(Action<T> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (_invokeDepth > 0)
            {
                _pending.Add(new PendingOp { Handler = handler, IsAdd = true });
                return;
            }

            if (!_handlers.Contains(handler))
            {
                _handlers.Add(handler);
            }
        }

        public void Remove(Action<T> handler)
        {
            if (handler == null)
            {
                return;
            }

            if (_invokeDepth > 0)
            {
                _pending.Add(new PendingOp { Handler = handler, IsAdd = false });
                return;
            }

            _handlers.Remove(handler);
        }

        public void Clear()
        {
            _handlers.Clear();
            _pending.Clear();
        }

        /// <summary>按操作队列里该回调的最后一次操作判断：最后一次是 Remove 则本轮分发跳过它。</summary>
        bool IsPendingRemove(Action<T> handler)
        {
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].Handler == handler)
                {
                    return !_pending[i].IsAdd;
                }
            }

            return false;
        }

        void ApplyPendingChanges()
        {
            for (var i = 0; i < _pending.Count; i++)
            {
                var op = _pending[i];
                if (op.IsAdd)
                {
                    if (!_handlers.Contains(op.Handler))
                    {
                        _handlers.Add(op.Handler);
                    }
                }
                else
                {
                    _handlers.Remove(op.Handler);
                }
            }

            _pending.Clear();
        }
    }
}
