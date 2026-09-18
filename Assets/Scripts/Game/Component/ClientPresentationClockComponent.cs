using Unity.Entities;
using Unity.Mathematics;

public struct ClientPresentationClockComponent : IComponentData
{
    public uint LatestAppliedFrame;
    public float FrameIntervalSeconds;
    public double AppliedAtRealtime;
    public uint Revision;
    public uint LastConsumedEventSequence;
}

public static class ClientPresentationTimeUtility
{
    public static float GetRemainingSeconds(
        in ClientPresentationClockComponent clock,
        uint endFrame,
        double realtime)
    {
        if (endFrame == uint.MaxValue)
            return -1f;

        if (endFrame <= clock.LatestAppliedFrame)
            return 0f;

        double frameRemaining = (endFrame - clock.LatestAppliedFrame) * clock.FrameIntervalSeconds;
        double elapsedSinceApply = math.max(0d, realtime - clock.AppliedAtRealtime);
        return (float)math.max(0d, frameRemaining - elapsedSinceApply);
    }
}
