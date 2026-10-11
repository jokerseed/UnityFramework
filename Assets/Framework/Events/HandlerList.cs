using System;
using System.Collections.Generic;
using Framework.Logging;

namespace Framework.Events
{
    /// <summary>
    /// 单个事件类型的监听列表。
    /// 分发过程中延迟增删，避免遍历时修改列表（参考 TEngine EventDelegateData）。
    /// </summary>
    internal sealed class HandlerList<T> : IEventChannel where T : struct
    {
        readonly List<Action<T>> _handlers = new List<Action<T>>(4);
        readonly List<Action<T>> _pendingAdds = new List<Action<T>>(2);
        readonly List<Action<T>> _pendingRemoves = new List<Action<T>>(2);
        int _invokeDepth;
        bool _invoking;
        bool _dirty;

        public void Invoke(in T evt)
        {
            _invokeDepth++;
            _invoking = true;
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
                if (_invokeDepth > 0)
                {
                    _invokeDepth--;
                }

                if (_invokeDepth == 0)
                {
                    _invoking = false;
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

            if (_invoking)
            {
                _dirty = true;
                _pendingAdds.Add(handler);
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

            if (_invoking)
            {
                _dirty = true;
                _pendingRemoves.Add(handler);
                return;
            }

            _handlers.Remove(handler);
        }

        public void Clear()
        {
            _handlers.Clear();
            _pendingAdds.Clear();
            _pendingRemoves.Clear();
            _dirty = false;
            _invokeDepth = 0;
            _invoking = false;
        }

        bool IsPendingRemove(Action<T> handler)
        {
            for (var i = 0; i < _pendingRemoves.Count; i++)
            {
                if (_pendingRemoves[i] == handler)
                {
                    return true;
                }
            }

            return false;
        }

        void ApplyPendingChanges()
        {
            if (!_dirty)
            {
                return;
            }

            for (var i = 0; i < _pendingAdds.Count; i++)
            {
                var handler = _pendingAdds[i];
                if (!_handlers.Contains(handler))
                {
                    _handlers.Add(handler);
                }
            }

            _pendingAdds.Clear();

            for (var i = 0; i < _pendingRemoves.Count; i++)
            {
                _handlers.Remove(_pendingRemoves[i]);
            }

            _pendingRemoves.Clear();
            _dirty = false;
        }
    }
}
