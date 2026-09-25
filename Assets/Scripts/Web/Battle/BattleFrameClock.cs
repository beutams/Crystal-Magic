using System;

namespace Server
{
    // 只累计现实时间；逻辑帧号由 ECS 完成整轮模拟后推进。
    public sealed class BattleFrameClock
    {
        public double AccumulatedMilliseconds { get; private set; }
        public long LastUpdateTime { get; private set; }

        public void Reset(long now)
        {
            AccumulatedMilliseconds = 0;
            LastUpdateTime = now;
        }

        public void Advance(long now, double speed)
        {
            AccumulatedMilliseconds += Math.Max(0, now - LastUpdateTime) * speed;
            LastUpdateTime = now;
        }

        public bool TakeFrame(int interval, bool catchUp)
        {
            if (AccumulatedMilliseconds >= interval)
            {
                AccumulatedMilliseconds -= interval;
                return true;
            }

            return catchUp;
        }

        public double GetFrameElapsedMilliseconds(long now, int interval)
        {
            return Math.Min(interval, AccumulatedMilliseconds + Math.Max(0, now - LastUpdateTime));
        }
    }
}
