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
    public byte NetworkDirty;
}

[UnitSourceProvider(typeof(UnitControlRuntimeComponent), typeof(UnitControlAuthoring))]
public static class UnitControlSource
{
    [UnitSourceGet(0, "unit.control.entryCount", UnitValueCategory.Number)]
    [UnitSourceGet(1, "unit.control.hasControl", UnitValueCategory.Bool)]
    [UnitSourceGet(2, "unit.control.activeType", UnitValueCategory.Number)]
    [UnitSourceGet(3, "unit.control.activeRemainingTime", UnitValueCategory.Number)]
    [UnitSourceGet(4, "unit.control.activePriority", UnitValueCategory.Number)]
    [UnitSourceGet(5, "unit.control.lockMove", UnitValueCategory.Bool)]
    [UnitSourceGet(6, "unit.control.lockCast", UnitValueCategory.Bool)]
    [UnitSourceGet(7, "unit.control.activeSourceEntity", UnitValueCategory.Entity)]
    [UnitSourceGet(8, "unit.control.activeMotionVelocity", UnitValueCategory.Float2)]
    [UnitSourceGet(9, "unit.control.activeMotionDamping", UnitValueCategory.Number)]
    public static bool TryGet(
        int operation,
        in UnitControlRuntimeComponent value,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = operation switch
        {
            0 => UnitSourceValue.FromInt(value.Entries.Length),
            1 => UnitSourceValue.FromBool(value.HasControl != 0),
            2 => UnitSourceValue.FromInt((int)value.ActiveType),
            3 => UnitSourceValue.FromFloat(value.ActiveRemainingTime),
            4 => UnitSourceValue.FromInt(value.ActivePriority),
            5 => UnitSourceValue.FromBool(value.LockMove != 0),
            6 => UnitSourceValue.FromBool(value.LockCast != 0),
            7 => UnitSourceValue.FromEntity(value.ActiveSourceEntity),
            8 => UnitSourceValue.FromFloat2(value.ActiveMotionVelocity),
            9 => UnitSourceValue.FromFloat(value.ActiveMotionDamping),
            _ => UnitSourceValue.None,
        };
        return result.Type != UnitValueType.None;
    }

    [UnitSourceGet(10, "unit.control.entryTypeAt", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(11, "unit.control.entryRemainingTimeAt", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(12, "unit.control.entryPriorityAt", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(13, "unit.control.entryLockMoveAt", UnitValueCategory.Bool, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(14, "unit.control.entryLockCastAt", UnitValueCategory.Bool, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(15, "unit.control.entryInterruptOnApplyAt", UnitValueCategory.Bool, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(16, "unit.control.entrySourceEntityAt", UnitValueCategory.Entity, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(17, "unit.control.entryMotionVelocityAt", UnitValueCategory.Float2, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(18, "unit.control.entryMotionDampingAt", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    public static bool TryGetEntry(
        int operation,
        in UnitControlRuntimeComponent value,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = default;
        if (!arguments.TryGetInt(0, out int index) || index < 0 || index >= value.Entries.Length)
            return false;

        UnitControlRuntimeEntry entry = value.Entries[index];
        result = operation switch
        {
            10 => UnitSourceValue.FromInt((int)entry.ControlType),
            11 => UnitSourceValue.FromFloat(entry.RemainingTime),
            12 => UnitSourceValue.FromInt(entry.Priority),
            13 => UnitSourceValue.FromBool(entry.LockMove != 0),
            14 => UnitSourceValue.FromBool(entry.LockCast != 0),
            15 => UnitSourceValue.FromBool(entry.InterruptOnApply != 0),
            16 => UnitSourceValue.FromEntity(entry.SourceEntity),
            17 => UnitSourceValue.FromFloat2(entry.MotionVelocity),
            18 => UnitSourceValue.FromFloat(entry.MotionDamping),
            _ => UnitSourceValue.None,
        };
        return result.Type != UnitValueType.None;
    }
}
