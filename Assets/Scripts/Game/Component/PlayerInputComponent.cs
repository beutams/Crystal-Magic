using CrystalMagic.Core;
using Unity.Entities;
using Unity.Mathematics;

public struct PlayerInputComponent : IComponentData
{
    public float2 Move;
    public float3 PointerWorldPosition;
    public byte IsPrimaryHeld;
    public byte IsInteractHeld;
    public byte IsInventoryHeld;
    public byte IsPropertyHeld;
    public byte IsEscapeHeld;
    public byte IsSkillHeld;
    public int SkillChainIndex;
    public byte IsNextSkillChainHeld;
    public byte IsUsePropHeld;
    public int PropIndex;
}

[UnitSourceAuthoring(typeof(PlayerCurrentSkillAuthoring))]
public sealed class PlayerInputSource : UnitComponentSource<PlayerInputComponent>
{
    protected override void Define(UnitSourceDefinitionBuilder<PlayerInputComponent> builder)
    {
        builder.AddGet("player.input.move", UnitValueCategory.Float2,
            (in PlayerInputComponent value) => UnitValue.FromFloat2(value.Move));
        builder.AddGet("player.input.pointerWorldPosition", UnitValueCategory.Float3,
            (in PlayerInputComponent value) => UnitValue.FromFloat3(value.PointerWorldPosition));
        builder.AddGet("player.input.primaryHeld", UnitValueCategory.Bool,
            (in PlayerInputComponent value) => UnitValue.FromBool(value.IsPrimaryHeld != 0));
        builder.AddGet("player.input.interactHeld", UnitValueCategory.Bool,
            (in PlayerInputComponent value) => UnitValue.FromBool(value.IsInteractHeld != 0));
        builder.AddGet("player.input.inventoryHeld", UnitValueCategory.Bool,
            (in PlayerInputComponent value) => UnitValue.FromBool(value.IsInventoryHeld != 0));
        builder.AddGet("player.input.propertyHeld", UnitValueCategory.Bool,
            (in PlayerInputComponent value) => UnitValue.FromBool(value.IsPropertyHeld != 0));
        builder.AddGet("player.input.escapeHeld", UnitValueCategory.Bool,
            (in PlayerInputComponent value) => UnitValue.FromBool(value.IsEscapeHeld != 0));
        builder.AddGet("player.input.skillHeld", UnitValueCategory.Bool,
            (in PlayerInputComponent value) => UnitValue.FromBool(value.IsSkillHeld != 0));
        builder.AddGet("player.input.skillChainIndex", UnitValueCategory.Number,
            (in PlayerInputComponent value) => UnitValue.FromInt(value.SkillChainIndex));
        builder.AddGet("player.input.nextSkillChainHeld", UnitValueCategory.Bool,
            (in PlayerInputComponent value) => UnitValue.FromBool(value.IsNextSkillChainHeld != 0));
        builder.AddGet("player.input.usePropHeld", UnitValueCategory.Bool,
            (in PlayerInputComponent value) => UnitValue.FromBool(value.IsUsePropHeld != 0));
        builder.AddGet("player.input.propIndex", UnitValueCategory.Number,
            (in PlayerInputComponent value) => UnitValue.FromInt(value.PropIndex));
    }
}
