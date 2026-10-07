using System;
using Unity.Core;
using Unity.Entities;

namespace Server
{
    // 执行间隔可以改变，传给系统的模拟步长始终不变。
    public sealed class BattleFrameRateManager : IRateManager
    {
        private readonly FrameManager frame;
        private readonly Func<long> timeProvider;
        private bool pushedTime;
        private int steps;
        private long updateTime;
        private double catchUpTarget;

        public int MaxStepsPerUpdate { get; set; } = 4;
        public int LastStepCount { get; private set; }
        public float Timestep { get => frame.frameInterval / 1000f; set => frame.frameInterval = Math.Max(1, (int)Math.Round(value * 1000)); }

        public BattleFrameRateManager(FrameManager frame, Func<long> timeProvider = null)
        {
            this.frame = frame;
            this.timeProvider = timeProvider ?? (() => NetworkTimer.Instance.TimeNow);
        }

        public bool ShouldGroupUpdate(ComponentSystemGroup group)
        {
            if (pushedTime)
            {
                group.World.PopTime();
                pushedTime = false;
                if (frame.running)
                    frame.OnTick();
                else
                    return false;
            }
            else
            {
                steps = 0;
                LastStepCount = 0;
                updateTime = timeProvider();
                catchUpTarget = 0;
                if (!frame.running)
                {
                    // World initialization runs outside this player-only group.
                    frame.clock.Reset(updateTime);
                    return false;
                }

                float gameDeltaTime = group.World.Time.DeltaTime;
                if (gameDeltaTime <= 0)
                    return false;
                double speed = 1;
                if (frame is ClientFrameManager client)
                {
                    speed = client.UpdateSimulationSpeed(updateTime);
                    if (client.FrameSpeedAdjustmentEnabled && client.HasFreshServerClock(updateTime) &&
                        frame.currentFrame <= client.EstimatedServerFrame)
                        catchUpTarget = Math.Ceiling(client.EstimatedServerFrame + client.TargetAheadFrames);
                }
                frame.clock.AdvanceGameTime(gameDeltaTime * 1000d, updateTime, speed);
            }

            if (!frame.running || steps >= MaxStepsPerUpdate ||
                !frame.clock.TakeFrame(frame.frameInterval, frame.currentFrame < catchUpTarget))
                return false;

            steps++;
            LastStepCount = steps;
            group.World.PushTime(new TimeData((frame.currentFrame + 1d) * Timestep, Timestep));
            pushedTime = true;
            return true;
        }
    }
}
