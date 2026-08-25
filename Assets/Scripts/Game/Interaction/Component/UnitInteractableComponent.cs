using CrystalMagic.Game.Data;
using Unity.Entities;

public enum InteractionKind : byte
{
    None = 0,
    Drop = 1,
    Treasure = 2,
    Npc = 3,
}

public struct UnitInteractionData
{
    public InteractionKind Kind;
    public int DataId;
    public int Amount;
    public int Variant;

    public bool IsValid => Kind != InteractionKind.None;

    public static UnitInteractionData CreateDrop(DropRewardType dropType, int itemId, int amount)
    {
        return new UnitInteractionData
        {
            Kind = InteractionKind.Drop,
            DataId = itemId,
            Amount = amount,
            Variant = (int)dropType,
        };
    }
}

public struct UnitInteractableComponent : IComponentData
{
    public UnitInteractionData Data;
    public float RangeSq;
    public byte IsEnabled;
}
