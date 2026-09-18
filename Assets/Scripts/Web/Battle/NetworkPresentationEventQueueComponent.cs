using System.Collections.Generic;
using Unity.Entities;

public sealed class NetworkPresentationEventQueueComponent : IComponentData
{
    public readonly List<NetworkPresentationEventStateData> Events = new();
    public uint NextSequence;
}
