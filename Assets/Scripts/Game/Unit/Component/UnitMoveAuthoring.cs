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
