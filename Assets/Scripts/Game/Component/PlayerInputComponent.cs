using CrystalMagic.Core;
using Unity.Entities;
using Unity.Mathematics;

public struct PlayerInputComponent : IComponentData
{
    public const string SkillChainChangedEventName = "Player.Input.SkillChain.Changed";

    public float2 Move;
    public float3 PointerWorldPosition;
    public byte IsPrimaryHeld;
    public byte IsInteractHeld;
    public byte IsInventoryHeld;
    public byte IsPropertyHeld;
    public byte IsEscapeHeld;
    public byte IsSkillHeld;
    public int SkillChainIndex;
    public byte IsUsePropHeld;
    public int PropIndex;
    public byte NetworkDirty;
}

public static class PlayerInputUtility
{
    public static int GetSkillChainIndex()
    {
        return GameRuntimeStateUtility.TryGetPlayerEntity(out EntityManager entityManager, out Entity player) &&
               entityManager.HasComponent<PlayerInputComponent>(player)
            ? entityManager.GetComponentData<PlayerInputComponent>(player).SkillChainIndex
            : 0;
    }
}

[UnitSourceProvider(typeof(PlayerInputComponent), typeof(PlayerInputAuthoring))]
public static class PlayerInputSource
{
    [UnitSourceGet(0, "player.input.move", UnitValueCategory.Float2)]
    [UnitSourceGet(1, "player.input.pointerWorldPosition", UnitValueCategory.Float3)]
    [UnitSourceGet(2, "player.input.primaryHeld", UnitValueCategory.Bool)]
    [UnitSourceGet(3, "player.input.interactHeld", UnitValueCategory.Bool)]
    [UnitSourceGet(4, "player.input.inventoryHeld", UnitValueCategory.Bool)]
    [UnitSourceGet(5, "player.input.propertyHeld", UnitValueCategory.Bool)]
    [UnitSourceGet(6, "player.input.escapeHeld", UnitValueCategory.Bool)]
    [UnitSourceGet(7, "player.input.skillHeld", UnitValueCategory.Bool)]
    [UnitSourceGet(8, "player.input.skillChainIndex", UnitValueCategory.Number)]
    [UnitSourceGet(10, "player.input.usePropHeld", UnitValueCategory.Bool)]
    [UnitSourceGet(11, "player.input.propIndex", UnitValueCategory.Number)]
    public static bool TryGet(
        int operation,
        in PlayerInputComponent value,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = operation switch
        {
            0 => UnitSourceValue.FromFloat2(value.Move),
            1 => UnitSourceValue.FromFloat3(value.PointerWorldPosition),
            2 => UnitSourceValue.FromBool(value.IsPrimaryHeld != 0),
            3 => UnitSourceValue.FromBool(value.IsInteractHeld != 0),
            4 => UnitSourceValue.FromBool(value.IsInventoryHeld != 0),
            5 => UnitSourceValue.FromBool(value.IsPropertyHeld != 0),
            6 => UnitSourceValue.FromBool(value.IsEscapeHeld != 0),
            7 => UnitSourceValue.FromBool(value.IsSkillHeld != 0),
            8 => UnitSourceValue.FromInt(value.SkillChainIndex),
            10 => UnitSourceValue.FromBool(value.IsUsePropHeld != 0),
            11 => UnitSourceValue.FromInt(value.PropIndex),
            _ => UnitSourceValue.None,
        };
        return result.Type != UnitValueType.None;
    }
}
