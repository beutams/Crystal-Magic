using System;
using CrystalMagic.Game.Data;
using Unity.Entities;

[Serializable]
public sealed class NetworkTreasureStateData : NetworkStateData
{
    public int regionId;
    public uint randomSeed;
    public byte interestSize;
    public DungeonTreasureQuality quality;
    public byte isOpened;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (context.TryGetEntity(unitId, out Entity entity))
            context.SetOrAdd(entity, new TreasureComponent
            {
                RegionId = regionId,
                RandomSeed = randomSeed,
                InterestSize = interestSize,
                Quality = quality,
                IsOpened = isOpened,
                NetworkDirty = 0,
            });
    }
}
