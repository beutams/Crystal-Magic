using System;
using Unity.Entities;

[Serializable]
public sealed class NetworkTreasureStateData : NetworkStateData
{
    public int regionId;
    public uint randomSeed;
    public byte interestSize;
    public byte isOpened;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (context.TryGetEntity(unitId, out Entity entity))
            context.SetOrAdd(entity, new TreasureComponent
            {
                RegionId = regionId,
                RandomSeed = randomSeed,
                InterestSize = interestSize,
                IsOpened = isOpened,
                NetworkDirty = 0,
            });
    }
}
