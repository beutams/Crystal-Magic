using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

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
                SpeedFactor = 1f,
                SpeedBonus = 0f,
                DesiredDirection = float2.zero,
                DesiredMaxSpeed = 0f,
                DesiredAcceleration = math.max(0f, baseAccel),
                Velocity = float2.zero,
            });
            AddComponent(entity, new UnitFacingComponent
            {
                Direction = new float2(1f, 0f),
            });
        }
    }
}

public struct UnitMoveComponent : IComponentData
{
    public float BaseMoveSpeed;
    public float BaseMoveSpeedOffset;
    public float BaseMaxAcceleration;
    public float SpeedFactor;
    public float SpeedBonus;
    public float2 DesiredDirection;
    public float DesiredMaxSpeed;
    public float DesiredAcceleration;
    public float2 Velocity;

    public float RealMoveSpeed => (BaseMoveSpeed + BaseMoveSpeedOffset) * SpeedFactor + SpeedBonus;
    public float RealMaxAcceleration => BaseMaxAcceleration * SpeedFactor + SpeedBonus;

    public float GetRealMoveSpeed(float commandSpeedFactor)
    {
        return RealMoveSpeed * math.max(0f, commandSpeedFactor);
    }

    public float GetRealMaxAcceleration(float commandSpeedFactor)
    {
        return RealMaxAcceleration * math.max(0f, commandSpeedFactor);
    }

    public void SetTargetMovement(float2 direction, float maxSpeed, float acceleration)
    {
        float2 normalizedDirection = math.normalizesafe(direction, float2.zero);
        DesiredDirection = normalizedDirection;
        DesiredMaxSpeed = math.max(0f, maxSpeed);
        DesiredAcceleration = math.max(0f, acceleration);

        if (math.lengthsq(normalizedDirection) <= 0.0001f || DesiredMaxSpeed <= 0.0001f)
        {
            DesiredDirection = float2.zero;
            DesiredMaxSpeed = 0f;
        }
    }

    public void SetTargetMovementByFactor(float2 direction, float speedFactor = 1f)
    {
        float safeSpeedFactor = math.max(0f, speedFactor);
        SetTargetMovement(direction,GetRealMoveSpeed(safeSpeedFactor),GetRealMaxAcceleration(safeSpeedFactor));
    }

    public void ClearTargetMovement()
    {
        DesiredDirection = float2.zero;
        DesiredMaxSpeed = 0f;
        DesiredAcceleration = math.max(0f, RealMaxAcceleration);
    }
}

[UnitSourceAuthoring(typeof(UnitMoveAuthoring))]
public sealed class UnitMoveSource : UnitComponentSource<UnitMoveComponent>
{
    private static readonly ComparatorParameterDefinition[] s_setTargetMovementParameters =
    {
        new ComparatorParameterDefinition("Direction", UnitValueCategory.Float2),
        new ComparatorParameterDefinition("MaxSpeed", UnitValueCategory.Number),
        new ComparatorParameterDefinition("Acceleration", UnitValueCategory.Number),
    };

    private static readonly ComparatorParameterDefinition[] s_setTargetMovementByFactorParameters =
    {
        new ComparatorParameterDefinition("Direction", UnitValueCategory.Float2),
        new ComparatorParameterDefinition("SpeedFactor", UnitValueCategory.Number),
    };

    protected override void Define(UnitSourceDefinitionBuilder<UnitMoveComponent> builder)
    {
        builder.AddGet("unit.move.baseMoveSpeed", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.BaseMoveSpeed));
        builder.AddGet("unit.move.baseMoveSpeedOffset", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.BaseMoveSpeedOffset));
        builder.AddGet("unit.move.baseMaxAcceleration", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.BaseMaxAcceleration));
        builder.AddGet("unit.move.speedFactor", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.SpeedFactor));
        builder.AddGet("unit.move.speedBonus", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.SpeedBonus));
        builder.AddGet("unit.move.realMoveSpeed", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.RealMoveSpeed));
        builder.AddGet("unit.move.realMaxAcceleration", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.RealMaxAcceleration));
        builder.AddGet("unit.move.desiredDirection", UnitValueCategory.Float2, (in UnitMoveComponent value) => UnitValue.FromFloat2(value.DesiredDirection));
        builder.AddGet("unit.move.desiredMaxSpeed", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.DesiredMaxSpeed));
        builder.AddGet("unit.move.desiredAcceleration", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.DesiredAcceleration));
        builder.AddGet("unit.move.velocity", UnitValueCategory.Float2, (in UnitMoveComponent value) => UnitValue.FromFloat2(value.Velocity));
        builder.AddGet("unit.move.isMoving", UnitValueCategory.Bool, (in UnitMoveComponent value) => UnitValue.FromBool(math.lengthsq(value.Velocity) > 0.0001f));

        builder.AddSet("unit.move.setTargetMovement", s_setTargetMovementParameters,
            (ref UnitMoveComponent value, UnitValue[] input) =>
            {
                if (!input[1].TryGetNumber(out float maxSpeed) || !input[2].TryGetNumber(out float acceleration))
                    return false;

                value.SetTargetMovement(input[0].Float2, maxSpeed, acceleration);
                return true;
            });
        builder.AddSet("unit.move.setTargetMovementByFactor", s_setTargetMovementByFactorParameters,
            (ref UnitMoveComponent value, UnitValue[] input) =>
            {
                if (!input[1].TryGetNumber(out float speedFactor))
                    return false;

                value.SetTargetMovementByFactor(input[0].Float2, speedFactor);
                return true;
            });
        builder.AddSet("unit.move.clearTargetMovement", System.Array.Empty<ComparatorParameterDefinition>(),
            (ref UnitMoveComponent value, UnitValue[] _) =>
            {
                value.ClearTargetMovement();
                return true;
            });
    }
}
