using System;

namespace Server
{
    // Game deltaTime drives simulation; wall time is only used for network clock samples.
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

        public void AdvanceGameTime(double deltaMilliseconds, long now, double speed)
        {
            AccumulatedMilliseconds += Math.Max(0, deltaMilliseconds) * speed;
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
