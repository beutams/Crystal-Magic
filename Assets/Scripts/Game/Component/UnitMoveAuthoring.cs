using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[RequireComponent(typeof(UnitFacingAuthoring))]
public class UnitMoveAuthoring : MonoBehaviour
{
    class UnitMoveBaker : Baker<UnitMoveAuthoring>
    {
        public override void Bake(UnitMoveAuthoring authoring)
        {
            TextAsset unitDataAsset = UnitAuthoringUtility.GetUnitDataTableAsset();
            if (unitDataAsset != null)
                DependsOn(unitDataAsset);

            float baseSpeed = 5f;
            float baseAccel = 30f;
            UnitMoveModuleData data = UnitAuthoringUtility.ResolveModuleData<UnitMoveModuleData>(authoring);
            if (data != null)
            {
                baseSpeed = data.BaseMoveSpeed;
                baseAccel = data.BaseMaxAcceleration;
            }

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitMoveComponent
            {
                BaseMoveSpeed = baseSpeed,
                BaseMoveSpeedOffset = 0f,
                BaseMaxAcceleration = baseAccel,
                Direction = float2.zero,
                StateMoveMultiplier = 1f,
                Velocity = float2.zero,
                FrameVelocity = float2.zero,
                HasFrameVelocity = 0,
                CommandMoveSpeed = -1f,
            });
        }
    }
}

public struct UnitMoveComponent : IComponentData
{
    public float BaseMoveSpeed;
    public float BaseMoveSpeedOffset;
    public float BaseMaxAcceleration;
    public float2 Direction;
    public float StateMoveMultiplier;
    public float2 Velocity;
    public float2 FrameVelocity;
    public byte HasFrameVelocity;
    // A non-negative value is an externally commanded speed. -1 means use
    // the normal modifier-resolved unit speed.
    public float CommandMoveSpeed;
    public byte NetworkDirty;

    public float BaseMoveSpeedValue => BaseMoveSpeed + BaseMoveSpeedOffset;
}

[UnitSourceProvider(typeof(UnitMoveComponent), typeof(UnitMoveAuthoring))]
public static class UnitMoveSource
{
    [UnitSourceGet(0, "unit.move.baseMoveSpeed", UnitValueCategory.Number)]
    [UnitSourceGet(1, "unit.move.baseMaxAcceleration", UnitValueCategory.Number)]
    [UnitSourceGet(2, "unit.move.direction", UnitValueCategory.Float2)]
    [UnitSourceGet(3, "unit.move.stateMoveMultiplier", UnitValueCategory.Number)]
    [UnitSourceGet(4, "unit.move.commandSpeed", UnitValueCategory.Number)]
    public static bool TryGet(
        int operation,
        in UnitMoveComponent value,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = operation switch
        {
            0 => UnitSourceValue.FromFloat(value.BaseMoveSpeedValue),
            1 => UnitSourceValue.FromFloat(value.BaseMaxAcceleration),
            2 => UnitSourceValue.FromFloat2(value.Direction),
            3 => UnitSourceValue.FromFloat(value.StateMoveMultiplier),
            4 => UnitSourceValue.FromFloat(value.CommandMoveSpeed),
            _ => UnitSourceValue.None,
        };
        return result.Type != UnitValueType.None;
    }

    [UnitSourceGet(5, "unit.move.realMoveSpeed", UnitValueCategory.Number)]
    [UnitSourceGet(6, "unit.move.realMaxAcceleration", UnitValueCategory.Number)]
    public static bool TryGetResolved(
        int operation,
        EntityManager entityManager,
        Entity entity,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        if (!entityManager.Exists(entity) || !entityManager.HasComponent<UnitMoveComponent>(entity))
        {
            result = default;
            return false;
        }

        result = operation switch
        {
            5 => UnitSourceValue.FromFloat(UnitModifierResolver.GetMoveSpeed(entityManager, entity)),
            6 => UnitSourceValue.FromFloat(UnitModifierResolver.GetMaxAcceleration(entityManager, entity)),
            _ => UnitSourceValue.None,
        };
        return result.Type != UnitValueType.None;
    }

    [UnitSourceSet(0, "unit.move.setDirection", UnitValueCategory.Float2, ParameterNames = new[] { "Direction" })]
    [UnitSourceSet(1, "unit.move.setVelocity", UnitValueCategory.Float2, ParameterNames = new[] { "Velocity" })]
    [UnitSourceSet(2, "unit.move.setFrameVelocity", UnitValueCategory.Float2, ParameterNames = new[] { "Velocity" })]
    [UnitSourceSet(3, "unit.move.setStateMoveMultiplier", UnitValueCategory.Number, ParameterNames = new[] { "Multiplier" })]
    [UnitSourceSet(4, "unit.move.setCommandSpeed", UnitValueCategory.Number, ParameterNames = new[] { "Speed" })]
    public static bool TrySet(int operation, ref UnitMoveComponent value, in UnitSourceArguments arguments)
    {
        switch (operation)
        {
            case 0 when arguments.TryGetFloat2(0, out float2 direction):
                value.Direction = direction;
                break;
            case 1 when arguments.TryGetFloat2(0, out float2 velocity):
                value.Velocity = velocity;
                break;
            case 2 when arguments.TryGetFloat2(0, out float2 frameVelocity):
                value.FrameVelocity = frameVelocity;
                value.HasFrameVelocity = 1;
                break;
            case 3 when arguments.TryGetNumber(0, out float multiplier):
                value.StateMoveMultiplier = math.max(0f, multiplier);
                break;
            case 4 when arguments.TryGetNumber(0, out float speed):
                value.CommandMoveSpeed = speed < 0f ? -1f : math.max(0f, speed);
                break;
            default:
                return false;
        }

        if (operation is 1 or 3)
            value.NetworkDirty = 1;
        return true;
    }
}
