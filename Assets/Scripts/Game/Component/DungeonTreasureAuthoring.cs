using Unity.Entities;
using UnityEngine;

public sealed class DungeonTreasureAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<DungeonTreasureAuthoring>
    {
        public override void Bake(DungeonTreasureAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new TreasureComponent
            {
                RegionId = -1,
                RandomSeed = 1u,
                InterestSize = 0,
                IsOpened = 0,
            });
            AddComponent(entity, new UnitInteractableComponent
            {
                Data = new UnitInteractionData
                {
                    Kind = InteractionKind.Treasure,
                    DataId = -1,
                },
                RangeSq = -1f,
                IsEnabled = 1,
            });
            AddBuffer<DungeonTreasureCandidateItemElement>(entity);
        }
    }
}

public struct TreasureComponent : IComponentData
{
    public int RegionId;
    public uint RandomSeed;
    public byte InterestSize;
    public byte IsOpened;
    public byte NetworkDirty;
}

[UnitSourceProvider(typeof(TreasureComponent), typeof(DungeonTreasureAuthoring))]
public static class TreasureSource
{
    [UnitSourceGet(0, "unit.treasure.opened", UnitValueCategory.Bool)]
    public static bool TryGet(
        int operation,
        in TreasureComponent value,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = operation == 0
            ? UnitSourceValue.FromBool(value.IsOpened != 0)
            : UnitSourceValue.None;
        return result.Type != UnitValueType.None;
    }

    [UnitSourceSet(0, "unit.treasure.setOpened", UnitValueCategory.Bool,
        ParameterNames = new[] { "Opened" })]
    public static bool TrySet(
        int operation,
        ref TreasureComponent value,
        in UnitSourceArguments arguments)
    {
        if (operation != 0 || !arguments.TryGetBool(0, out bool opened))
            return false;

        byte nextValue = opened ? (byte)1 : (byte)0;
        if (value.IsOpened == nextValue)
            return true;

        value.IsOpened = nextValue;
        value.NetworkDirty = 1;
        return true;
    }
}
