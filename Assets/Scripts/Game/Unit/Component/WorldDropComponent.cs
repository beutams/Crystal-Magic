using CrystalMagic.Game.Data;
using Unity.Entities;

public struct WorldDropComponent : IComponentData
{
    public DropRewardType DropType;
    public int ItemId;
    public int Amount;
}
