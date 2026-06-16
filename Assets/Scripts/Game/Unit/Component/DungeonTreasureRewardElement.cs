using CrystalMagic.Game.Data;
using Unity.Entities;

public struct DungeonTreasureRewardElement : IBufferElementData
{
    public DropRewardType RewardType;
    public int ItemId;
    public float Chance;
    public int MinQuantity;
    public int MaxQuantity;
}
