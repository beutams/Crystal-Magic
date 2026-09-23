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
    public byte NetworkDirty;
}

[UnitSourceProvider(typeof(UnitInteractableComponent), typeof(NPCInteractableAuthoring))]
public static class UnitInteractableSource
{
    [UnitSourceGet(0, "unit.interactable.enabled", UnitValueCategory.Bool)]
    [UnitSourceGet(1, "unit.interactable.rangeSq", UnitValueCategory.Number)]
    public static bool TryGet(
        int operation,
        in UnitInteractableComponent value,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = operation switch
        {
            0 => UnitSourceValue.FromBool(value.IsEnabled != 0),
            1 => UnitSourceValue.FromFloat(value.RangeSq),
            _ => UnitSourceValue.None,
        };
        return result.Type != UnitValueType.None;
    }

    [UnitSourceSet(0, "unit.interactable.setEnabled", UnitValueCategory.Bool,
        ParameterNames = new[] { "Enabled" })]
    public static bool TrySet(
        int operation,
        ref UnitInteractableComponent value,
        in UnitSourceArguments arguments)
    {
        if (operation != 0 || !arguments.TryGetBool(0, out bool enabled))
            return false;

        byte nextValue = enabled ? (byte)1 : (byte)0;
        if (value.IsEnabled == nextValue)
            return true;

        value.IsEnabled = nextValue;
        value.NetworkDirty = 1;
        return true;
    }
}
