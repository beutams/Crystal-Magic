using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace Server
{
    /// <summary>
    /// 由主循环调用 Update 驱动的单线程定时器。
    /// </summary>
    public sealed class TimerManager
    {
        private sealed class Timer
        {
            public long Id;
            public long Interval;
            public long NextTriggerTime;
            public bool Repeat;
            public Action Callback;
        }

        private static readonly TimerManager instance = new TimerManager();

        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly Dictionary<long, Timer> timers = new Dictionary<long, Timer>();
        private readonly List<long> dueTimerIds = new List<long>();

        private long idGenerator;

        public static TimerManager Instance => instance;

        /// <summary>从 TimerManager 创建起累计的单调毫秒时间。</summary>
        public long TimeNow => this.clock.ElapsedMilliseconds;

        private TimerManager()
        {
        }

        /// <summary>添加一次性定时器，返回用于取消的 id。</summary>
        public long AddOnce(long delayMilliseconds, Action callback)
        {
            if (delayMilliseconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delayMilliseconds));
            }

            return this.Add(delayMilliseconds, false, callback);
        }

        /// <summary>添加循环定时器，返回用于取消的 id。</summary>
        public long AddRepeated(long intervalMilliseconds, Action callback)
        {
            if (intervalMilliseconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalMilliseconds));
            }

            return this.Add(intervalMilliseconds, true, callback);
        }

        public bool Remove(long timerId)
        {
            return this.timers.Remove(timerId);
        }

        /// <summary>
        /// 每帧调用一次。重复定时器卡帧后只执行一次，并从当前时刻重新计时。
        /// </summary>
        public void Update()
        {
            long now = this.TimeNow;
            this.dueTimerIds.Clear();

            foreach (Timer timer in this.timers.Values)
            {
                if (now >= timer.NextTriggerTime)
                {
                    this.dueTimerIds.Add(timer.Id);
                }
            }

            foreach (long timerId in this.dueTimerIds)
            {
                if (!this.timers.TryGetValue(timerId, out Timer timer))
                {
                    continue;
                }

                if (timer.Repeat)
                {
                    timer.NextTriggerTime = now + timer.Interval;
                }
                else
                {
                    this.timers.Remove(timerId);
                }

                try
                {
                    timer.Callback?.Invoke();
                }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                }
            }
        }

        private long Add(long delayMilliseconds, bool repeat, Action callback)
        {
            if (callback == null)
            {
                throw new ArgumentNullException(nameof(callback));
            }

            long id = ++this.idGenerator;
            this.timers.Add(id, new Timer
            {
                Id = id,
                Interval = delayMilliseconds,
                NextTriggerTime = this.TimeNow + delayMilliseconds,
                Repeat = repeat,
                Callback = callback,
            });

            return id;
        }
    }
}
