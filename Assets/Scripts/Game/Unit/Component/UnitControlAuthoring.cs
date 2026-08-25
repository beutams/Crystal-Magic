using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public sealed class UnitControlAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<UnitControlAuthoring>
    {
        public override void Bake(UnitControlAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitControlRuntimeComponent
            {
                Entries = new FixedList512Bytes<UnitControlRuntimeEntry>(),
                ActiveType = UnitControlType.None,
                ActiveRemainingTime = 0f,
                ActivePriority = 0,
                LockMove = 0,
                LockCast = 0,
                HasControl = 0,
                ActiveSourceEntity = Entity.Null,
                ActiveMotionVelocity = float2.zero,
                ActiveMotionDamping = 0f,
            });
        }
    }
}

public enum UnitControlType : byte
{
    None = 0,
    Knockback = 1,
    Stun = 2,
    Fear = 3,
}

public struct UnitControlRuntimeEntry
{
    public UnitControlType ControlType;
    public float RemainingTime;
    public int Priority;
    public byte LockMove;
    public byte LockCast;
    public byte InterruptOnApply;
    public Entity SourceEntity;
    public float2 MotionVelocity;
    public float MotionDamping;
}

public struct UnitControlRuntimeComponent : IComponentData
{
    public FixedList512Bytes<UnitControlRuntimeEntry> Entries;
    public UnitControlType ActiveType;
    public float ActiveRemainingTime;
    public int ActivePriority;
    public byte LockMove;
    public byte LockCast;
    public byte HasControl;
    public Entity ActiveSourceEntity;
    public float2 ActiveMotionVelocity;
    public float ActiveMotionDamping;
}

[UnitSourceAuthoring(typeof(UnitControlAuthoring))]
public sealed class UnitControlSource : UnitComponentSource<UnitControlRuntimeComponent>
{
    private static readonly ComparatorParameterDefinition[] s_indexParameter =
    {
        new ComparatorParameterDefinition("Index", UnitValueCategory.Number),
    };

    protected override void Define(UnitSourceDefinitionBuilder<UnitControlRuntimeComponent> builder)
    {
        builder.AddGet("unit.control.entryCount", UnitValueCategory.Number,
            (in UnitControlRuntimeComponent value) => UnitValue.FromInt(value.Entries.Length));
        builder.AddGet("unit.control.hasControl", UnitValueCategory.Bool,
            (in UnitControlRuntimeComponent value) => UnitValue.FromBool(value.HasControl != 0));
        builder.AddGet("unit.control.activeType", UnitValueCategory.Number,
            (in UnitControlRuntimeComponent value) => UnitValue.FromInt((int)value.ActiveType));
        builder.AddGet("unit.control.activeRemainingTime", UnitValueCategory.Number,
            (in UnitControlRuntimeComponent value) => UnitValue.FromFloat(value.ActiveRemainingTime));
        builder.AddGet("unit.control.activePriority", UnitValueCategory.Number,
            (in UnitControlRuntimeComponent value) => UnitValue.FromInt(value.ActivePriority));
        builder.AddGet("unit.control.lockMove", UnitValueCategory.Bool,
            (in UnitControlRuntimeComponent value) => UnitValue.FromBool(value.LockMove != 0));
        builder.AddGet("unit.control.lockCast", UnitValueCategory.Bool,
            (in UnitControlRuntimeComponent value) => UnitValue.FromBool(value.LockCast != 0));
        builder.AddGet("unit.control.activeSourceEntity", UnitValueCategory.Entity,
            (in UnitControlRuntimeComponent value) => UnitValue.FromEntity(value.ActiveSourceEntity));
        builder.AddGet("unit.control.activeMotionVelocity", UnitValueCategory.Float2,
            (in UnitControlRuntimeComponent value) => UnitValue.FromFloat2(value.ActiveMotionVelocity));
        builder.AddGet("unit.control.activeMotionDamping", UnitValueCategory.Number,
            (in UnitControlRuntimeComponent value) => UnitValue.FromFloat(value.ActiveMotionDamping));

        builder.AddGet("unit.control.entryTypeAt", UnitValueCategory.Number, s_indexParameter,
            (in UnitControlRuntimeComponent value, UnitValue[] input) => GetEntry(value, input, out UnitControlRuntimeEntry entry) ? UnitValue.FromInt((int)entry.ControlType) : UnitValue.None);
        builder.AddGet("unit.control.entryRemainingTimeAt", UnitValueCategory.Number, s_indexParameter,
            (in UnitControlRuntimeComponent value, UnitValue[] input) => GetEntry(value, input, out UnitControlRuntimeEntry entry) ? UnitValue.FromFloat(entry.RemainingTime) : UnitValue.None);
        builder.AddGet("unit.control.entryPriorityAt", UnitValueCategory.Number, s_indexParameter,
            (in UnitControlRuntimeComponent value, UnitValue[] input) => GetEntry(value, input, out UnitControlRuntimeEntry entry) ? UnitValue.FromInt(entry.Priority) : UnitValue.None);
        builder.AddGet("unit.control.entryLockMoveAt", UnitValueCategory.Bool, s_indexParameter,
            (in UnitControlRuntimeComponent value, UnitValue[] input) => GetEntry(value, input, out UnitControlRuntimeEntry entry) ? UnitValue.FromBool(entry.LockMove != 0) : UnitValue.None);
        builder.AddGet("unit.control.entryLockCastAt", UnitValueCategory.Bool, s_indexParameter,
            (in UnitControlRuntimeComponent value, UnitValue[] input) => GetEntry(value, input, out UnitControlRuntimeEntry entry) ? UnitValue.FromBool(entry.LockCast != 0) : UnitValue.None);
        builder.AddGet("unit.control.entryInterruptOnApplyAt", UnitValueCategory.Bool, s_indexParameter,
            (in UnitControlRuntimeComponent value, UnitValue[] input) => GetEntry(value, input, out UnitControlRuntimeEntry entry) ? UnitValue.FromBool(entry.InterruptOnApply != 0) : UnitValue.None);
        builder.AddGet("unit.control.entrySourceEntityAt", UnitValueCategory.Entity, s_indexParameter,
            (in UnitControlRuntimeComponent value, UnitValue[] input) => GetEntry(value, input, out UnitControlRuntimeEntry entry) ? UnitValue.FromEntity(entry.SourceEntity) : UnitValue.None);
        builder.AddGet("unit.control.entryMotionVelocityAt", UnitValueCategory.Float2, s_indexParameter,
            (in UnitControlRuntimeComponent value, UnitValue[] input) => GetEntry(value, input, out UnitControlRuntimeEntry entry) ? UnitValue.FromFloat2(entry.MotionVelocity) : UnitValue.None);
        builder.AddGet("unit.control.entryMotionDampingAt", UnitValueCategory.Number, s_indexParameter,
            (in UnitControlRuntimeComponent value, UnitValue[] input) => GetEntry(value, input, out UnitControlRuntimeEntry entry) ? UnitValue.FromFloat(entry.MotionDamping) : UnitValue.None);
    }

    private static bool GetEntry(in UnitControlRuntimeComponent value, UnitValue[] input, out UnitControlRuntimeEntry entry)
    {
        entry = default;
        if (input == null || input.Length != 1 || !input[0].TryGetNumber(out float indexValue))
            return false;

        int index = (int)math.round(indexValue);
        if (math.abs(indexValue - index) > 0.0001f || index < 0 || index >= value.Entries.Length)
            return false;

        entry = value.Entries[index];
        return true;
    }
}
