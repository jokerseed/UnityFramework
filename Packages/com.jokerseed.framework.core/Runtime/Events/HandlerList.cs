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
        sealed class Subscription : IDisposable
        {
            readonly HandlerList<T> _owner;
            internal readonly Action<T> Handler;

            internal Subscription(HandlerList<T> owner, Action<T> handler)
            {
                _owner = owner;
                Handler = handler;
            }

            public void Dispose()
            {
                _owner.Remove(this);
            }
        }

        struct PendingOp
        {
            public Subscription Subscription;
            public bool IsAdd;
        }

        readonly List<Subscription> _handlers = new List<Subscription>(4);
        readonly List<PendingOp> _pending = new List<PendingOp>(4);
        int _invokeDepth;

        public void Invoke(in T evt)
        {
            _invokeDepth++;
            try
            {
                for (var i = 0; i < _handlers.Count; i++)
                {
                    var handler = _handlers[i].Handler;
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

        public IDisposable Add(Action<T> handler)
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            // 同一有效注册共享凭证；退订后重订必须创建新身份，旧凭证不能影响它。
            var subscription = FindCurrent(handler);
            if (subscription != null)
            {
                return subscription;
            }

            subscription = new Subscription(this, handler);
            if (_invokeDepth > 0)
            {
                _pending.Add(new PendingOp { Subscription = subscription, IsAdd = true });
            }
            else
            {
                _handlers.Add(subscription);
            }

            return subscription;
        }

        public void Remove(Action<T> handler)
        {
            if (handler != null)
            {
                Remove(FindCurrent(handler));
            }
        }

        void Remove(Subscription subscription)
        {
            if (subscription == null || FindCurrent(subscription.Handler) != subscription)
            {
                return;
            }

            if (_invokeDepth > 0)
            {
                _pending.Add(new PendingOp { Subscription = subscription, IsAdd = false });
            }
            else
            {
                _handlers.Remove(subscription);
            }
        }

        // 将待处理操作纳入查询，让分发中的重复订阅、退订和重订也遵守注册身份。
        Subscription FindCurrent(Action<T> handler)
        {
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                var op = _pending[i];
                if (op.Subscription.Handler == handler)
                {
                    return op.IsAdd ? op.Subscription : null;
                }
            }

            for (var i = 0; i < _handlers.Count; i++)
            {
                if (_handlers[i].Handler == handler)
                {
                    return _handlers[i];
                }
            }

            return null;
        }

        public void Clear()
        {
            _handlers.Clear();
            _pending.Clear();
        }

        /// <summary>
        /// 本轮快照仍按委托判断最后一次操作：Remove 后 Add 会恢复原处理器本轮的调用，
        /// 即使新注册拥有不同身份；只从未在快照出现过的处理器需等待最外层分发结束。
        /// </summary>
        bool IsPendingRemove(Action<T> handler)
        {
            for (var i = _pending.Count - 1; i >= 0; i--)
            {
                if (_pending[i].Subscription.Handler == handler)
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
                    _handlers.Add(op.Subscription);
                }
                else
                {
                    _handlers.Remove(op.Subscription);
                }
            }

            _pending.Clear();
        }
    }
}
