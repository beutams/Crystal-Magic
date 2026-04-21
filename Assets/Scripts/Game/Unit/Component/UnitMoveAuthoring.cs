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
            float baseSpeed = 5f;
            float baseAccel = 30f;
            UnitData data = UnitAuthoringUtility.ResolveUnitData(authoring);
            if (data != null)
            {
                baseSpeed = data.BaseMoveSpeed;
                baseAccel = data.BaseMaxAcceleration;
            }

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitMoveComponent
            {
                BaseMoveSpeed       = baseSpeed,
                BaseMaxAcceleration = baseAccel,
                SpeedFactor         = 1f,
                SpeedBonus          = 0f,
                AccelInput          = float2.zero,
                Velocity            = float2.zero,
            });
        }
    }
}

public struct UnitMoveComponent : IComponentData
{
    public float BaseMoveSpeed;
    public float BaseMaxAcceleration;
    public float SpeedFactor;
    public float SpeedBonus;
    public float2 AccelInput;
    public float2 Velocity;

    public float RealMoveSpeed => BaseMoveSpeed * SpeedFactor + SpeedBonus;
    public float RealMaxAcceleration => BaseMaxAcceleration * SpeedFactor + SpeedBonus;
}