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
        private bool simulated;
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
                if (simulated && frame.running)
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
                    // 黑屏初始化仍需推进烘焙/初始化系统，但不能消耗战斗时间。
                    frame.clock.Reset(updateTime);
                    group.World.PushTime(new TimeData(0, 0));
                    pushedTime = true;
                    simulated = false;
                    return true;
                }

                double speed = 1;
                if (frame is ClientFrameManager client)
                {
                    speed = client.UpdateSimulationSpeed(updateTime);
                    if (client.FrameSpeedAdjustmentEnabled && client.HasFreshServerClock(updateTime) &&
                        frame.currentFrame <= client.EstimatedServerFrame)
                        catchUpTarget = Math.Ceiling(client.EstimatedServerFrame + client.TargetAheadFrames);
                }
                frame.clock.Advance(updateTime, speed);
            }

            if (!frame.running || steps >= MaxStepsPerUpdate ||
                !frame.clock.TakeFrame(frame.frameInterval, frame.currentFrame < catchUpTarget))
                return false;

            steps++;
            LastStepCount = steps;
            group.World.PushTime(new TimeData((frame.currentFrame + 1d) * Timestep, Timestep));
            pushedTime = true;
            simulated = true;
            return true;
        }
    }
}
