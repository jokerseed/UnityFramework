using System;
using System.Collections.Generic;
using Framework.Core;
using Framework.Logging;
using UnityEngine;

namespace Framework.Timer
{
    /// <summary>
    /// 表现层定时器：延时、重复、下一帧回调，支持缩放/非缩放时间、暂停与绑定 Owner 生命周期。
    /// 新建的定时器从下一帧开始计时；回调异常只记日志，不影响其它定时器，抛异常的重复定时器会被取消。
    /// 一帧最多触发同一定时器一次，掉帧时不补发。
    /// 帧同步逻辑不要用它（依赖 Unity 帧时间，不确定）。
    /// </summary>
    public sealed class TimerManager : PersistentSingleton<TimerManager>
    {
        sealed class Entry
        {
            public int Version = 1;
            public bool Active;
            public int CreatedFrame;
            public bool Paused;
            public bool Unscaled;
            public bool HasOwner;
            public float Interval;
            public float Remaining;
            public int RepeatLeft;
            public Action Callback;
            public UnityEngine.Object Owner;
        }

        readonly List<Entry> _entries = new List<Entry>(64);
        readonly Stack<int> _free = new Stack<int>(64);
        int _activeCount;

        /// <summary>运行中（含暂停、待开始）的定时器数量。</summary>
        public int ActiveCount => _activeCount;

        /// <summary>延时执行一次。</summary>
        /// <param name="seconds">延时秒数；小于等于 0 时在下一帧执行。</param>
        /// <param name="callback">回调，不可为 null。</param>
        /// <param name="owner">绑定的 Unity 对象；被销毁后定时器自动取消。为 null 表示不绑定。</param>
        /// <param name="unscaled">为 true 时使用不受 timeScale 影响的时间。</param>
        /// <returns>定时器句柄。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> 为 null。</exception>
        public TimerHandle Delay(float seconds, Action callback, UnityEngine.Object owner = null, bool unscaled = false)
        {
            return Add(seconds, seconds, 1, callback, owner, unscaled);
        }

        /// <summary>按固定间隔重复执行。</summary>
        /// <param name="interval">间隔秒数；小于等于 0 时每帧执行一次。</param>
        /// <param name="callback">回调，不可为 null。</param>
        /// <param name="count">执行次数；小于等于 0 表示无限次。</param>
        /// <param name="firstDelay">首次执行前的延时；小于 0 时等于 <paramref name="interval"/>。</param>
        /// <param name="owner">绑定的 Unity 对象；被销毁后定时器自动取消。</param>
        /// <param name="unscaled">为 true 时使用不受 timeScale 影响的时间。</param>
        /// <returns>定时器句柄。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> 为 null。</exception>
        public TimerHandle Repeat(
            float interval,
            Action callback,
            int count = 0,
            float firstDelay = -1f,
            UnityEngine.Object owner = null,
            bool unscaled = false)
        {
            return Add(firstDelay < 0f ? interval : firstDelay, interval, count <= 0 ? -1 : count, callback, owner, unscaled);
        }

        /// <summary>下一帧执行一次。</summary>
        /// <param name="callback">回调，不可为 null。</param>
        /// <param name="owner">绑定的 Unity 对象；被销毁后不再执行。</param>
        /// <returns>定时器句柄。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="callback"/> 为 null。</exception>
        public TimerHandle NextFrame(Action callback, UnityEngine.Object owner = null)
        {
            return Add(0f, 0f, 1, callback, owner, true);
        }

        /// <summary>定时器是否仍在运行（含暂停、待开始）。</summary>
        /// <param name="handle">定时器句柄。</param>
        /// <returns>运行中返回 true。</returns>
        public bool IsActive(TimerHandle handle) => TryGet(handle, out _);

        /// <summary>取消定时器。</summary>
        /// <param name="handle">定时器句柄。</param>
        /// <returns>定时器仍在运行并被取消时返回 true。</returns>
        public bool Cancel(TimerHandle handle)
        {
            if (!TryGet(handle, out _))
            {
                return false;
            }

            Release(handle.Slot);
            return true;
        }

        /// <summary>暂停定时器。</summary>
        /// <param name="handle">定时器句柄。</param>
        /// <returns>定时器仍在运行时返回 true。</returns>
        public bool Pause(TimerHandle handle) => SetPaused(handle, true);

        /// <summary>恢复已暂停的定时器。</summary>
        /// <param name="handle">定时器句柄。</param>
        /// <returns>定时器仍在运行时返回 true。</returns>
        public bool Resume(TimerHandle handle) => SetPaused(handle, false);

        /// <summary>距离下次执行的剩余秒数；定时器已结束时返回 0。</summary>
        /// <param name="handle">定时器句柄。</param>
        /// <returns>剩余秒数。</returns>
        public float GetRemaining(TimerHandle handle) => TryGet(handle, out var entry) ? Mathf.Max(0f, entry.Remaining) : 0f;

        /// <summary>取消全部定时器。</summary>
        public void CancelAll()
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].Active)
                {
                    Release(i);
                }
            }
        }

        TimerHandle Add(float firstDelay, float interval, int repeat, Action callback, UnityEngine.Object owner, bool unscaled)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            int slot;
            if (_free.Count > 0)
            {
                slot = _free.Pop();
            }
            else
            {
                slot = _entries.Count;
                _entries.Add(new Entry());
            }

            var entry = _entries[slot];
            entry.Active = true;
            entry.CreatedFrame = Time.frameCount;
            entry.Paused = false;
            entry.Unscaled = unscaled;
            entry.HasOwner = owner != null;
            entry.Owner = owner;
            entry.Interval = interval;
            entry.Remaining = firstDelay;
            entry.RepeatLeft = repeat;
            entry.Callback = callback;
            _activeCount++;
            return new TimerHandle(slot, entry.Version);
        }

        bool SetPaused(TimerHandle handle, bool paused)
        {
            if (!TryGet(handle, out var entry))
            {
                return false;
            }

            entry.Paused = paused;
            return true;
        }

        bool TryGet(TimerHandle handle, out Entry entry)
        {
            if (handle.Slot >= 0 && handle.Slot < _entries.Count)
            {
                entry = _entries[handle.Slot];
                if (entry.Active && entry.Version == handle.Version)
                {
                    return true;
                }
            }

            entry = null;
            return false;
        }

        void Release(int slot)
        {
            var entry = _entries[slot];
            entry.Active = false;
            entry.Callback = null;
            entry.Owner = null;
            entry.Version++;
            _free.Push(slot);
            _activeCount--;
        }

        void Update() => Tick(Time.deltaTime, Time.unscaledDeltaTime, Time.frameCount);

        internal void Tick(float scaled, float unscaled, int frame)
        {
            var count = _entries.Count;
            for (var slot = 0; slot < count; slot++)
            {
                var entry = _entries[slot];
                if (!entry.Active || entry.Paused || entry.CreatedFrame >= frame)
                {
                    continue;
                }

                if (entry.HasOwner && entry.Owner == null)
                {
                    Release(slot);
                    continue;
                }

                entry.Remaining -= entry.Unscaled ? unscaled : scaled;
                if (entry.Remaining > 0f)
                {
                    continue;
                }

                Fire(slot, entry);
            }
        }

        void Fire(int slot, Entry entry)
        {
            var version = entry.Version;
            var callback = entry.Callback;
            var last = entry.RepeatLeft == 1;
            if (last)
            {
                Release(slot);
            }
            else
            {
                if (entry.RepeatLeft > 1)
                {
                    entry.RepeatLeft--;
                }

                entry.Remaining = entry.Interval > 0f ? Mathf.Max(entry.Remaining + entry.Interval, 0f) : 0f;
            }

            try
            {
                callback();
            }
            catch (Exception ex)
            {
                GameLog.Exception(LogCategories.Timer, ex, "Timer callback failed");
                if (!last && entry.Version == version)
                {
                    Release(slot);
                }
            }
        }

        /// <inheritdoc />
        protected override void OnDestroy()
        {
            CancelAll();
            base.OnDestroy();
        }
    }
}
