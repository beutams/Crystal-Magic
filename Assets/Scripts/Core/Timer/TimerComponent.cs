using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core
{
    public sealed class TimerComponent : GameComponent<TimerComponent>
    {
        private sealed class TimerEntry
        {
            public int Id;
            public float InitialDurationSeconds;
            public float RemainingSeconds;
            public bool IsActive;
            public bool UseUnscaledTime;
            public Action<float> OnTick;
            public Action OnCompleted;
        }

        private readonly List<TimerEntry> _timers = new();
        private int _nextTimerId = 1;

        public override int Priority => 15;

        public int Register(float durationSeconds, Action<float> onTick = null, Action onCompleted = null, bool useUnscaledTime = false)
        {
            float initialDurationSeconds = Mathf.Max(0f, durationSeconds);

            int timerId = _nextTimerId++;
            _timers.Add(new TimerEntry
            {
                Id = timerId,
                InitialDurationSeconds = initialDurationSeconds,
                RemainingSeconds = initialDurationSeconds,
                IsActive = false,
                UseUnscaledTime = useUnscaledTime,
                OnTick = onTick,
                OnCompleted = onCompleted,
            });

            return timerId;
        }

        public void Activate(int timerId)
        {
            TimerEntry timer = FindTimer(timerId);
            if (timer == null || timer.RemainingSeconds <= 0f)
                return;

            timer.IsActive = true;
        }

        public void Deactivate(int timerId)
        {
            TimerEntry timer = FindTimer(timerId);
            if (timer == null)
                return;

            timer.IsActive = false;
        }

        public void ResetTimer(int timerId, float? durationSeconds = null, bool activate = true)
        {
            TimerEntry timer = FindTimer(timerId);
            if (timer == null)
                return;

            if (durationSeconds.HasValue)
                timer.InitialDurationSeconds = Mathf.Max(0f, durationSeconds.Value);

            timer.RemainingSeconds = timer.InitialDurationSeconds;
            timer.IsActive = activate && timer.RemainingSeconds > 0f;
            timer.OnTick?.Invoke(timer.RemainingSeconds);
        }

        public void Cancel(int timerId)
        {
            if (timerId <= 0)
                return;

            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                if (_timers[i].Id != timerId)
                    continue;

                _timers.RemoveAt(i);
                return;
            }
        }

        public float GetRemainingSeconds(int timerId)
        {
            TimerEntry timer = FindTimer(timerId);
            return timer?.RemainingSeconds ?? 0f;
        }

        private TimerEntry FindTimer(int timerId)
        {
            if (timerId <= 0)
                return null;

            for (int i = 0; i < _timers.Count; i++)
            {
                if (_timers[i].Id == timerId)
                    return _timers[i];
            }

            return null;
        }

        public override void Cleanup()
        {
            _timers.Clear();
            _nextTimerId = 1;
            base.Cleanup();
        }

        private void Update()
        {
            for (int i = _timers.Count - 1; i >= 0; i--)
            {
                TimerEntry timer = _timers[i];
                if (!timer.IsActive)
                    continue;

                float deltaTime = timer.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                if (deltaTime <= 0f)
                    continue;

                timer.RemainingSeconds = Mathf.Max(0f, timer.RemainingSeconds - deltaTime);
                timer.OnTick?.Invoke(timer.RemainingSeconds);
                if (timer.RemainingSeconds > 0f)
                    continue;

                timer.IsActive = false;
                timer.OnCompleted?.Invoke();
            }
        }
    }
}
