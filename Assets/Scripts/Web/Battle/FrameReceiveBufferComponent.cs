using System.Collections.Generic;
using Unity.Entities;

// FrameManager only puts confirmed frames here. This component does not decide
// whether a frame is early, late, or needs prediction/rollback.
public sealed class FrameReceiveBufferComponent : IComponentData
{
    public int frameInterval = 33;
    public SortedDictionary<uint, Queue<NetworkState>> frames = new();
}
