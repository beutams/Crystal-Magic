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
                SpeedFactor = 1f,
                SpeedBonus = 0f,
                Direction = float2.zero,
                StateMoveMultiplier = 1f,
                Velocity = float2.zero,
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
    public float2 Direction;
    public float StateMoveMultiplier;
    public float2 Velocity;

    public float RealMoveSpeed => (BaseMoveSpeed + BaseMoveSpeedOffset) * SpeedFactor + SpeedBonus;
    public float RealMaxAcceleration => BaseMaxAcceleration * SpeedFactor + SpeedBonus;
}

[UnitSourceAuthoring(typeof(UnitMoveAuthoring))]
public sealed class UnitMoveSource : UnitComponentSource<UnitMoveComponent>
{
    protected override void Define(UnitSourceDefinitionBuilder<UnitMoveComponent> builder)
    {
        builder.AddGet("unit.move.baseMoveSpeed", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.BaseMoveSpeed));
        builder.AddGet("unit.move.baseMoveSpeedOffset", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.BaseMoveSpeedOffset));
        builder.AddGet("unit.move.baseMaxAcceleration", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.BaseMaxAcceleration));
        builder.AddGet("unit.move.speedFactor", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.SpeedFactor));
        builder.AddGet("unit.move.speedBonus", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.SpeedBonus));
        builder.AddGet("unit.move.realMoveSpeed", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.RealMoveSpeed));
        builder.AddGet("unit.move.realMaxAcceleration", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.RealMaxAcceleration));
        builder.AddGet("unit.move.direction", UnitValueCategory.Float2, (in UnitMoveComponent value) => UnitValue.FromFloat2(value.Direction));
        builder.AddGet("unit.move.stateMoveMultiplier", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.StateMoveMultiplier));
        builder.AddGet("unit.move.velocity", UnitValueCategory.Float2, (in UnitMoveComponent value) => UnitValue.FromFloat2(value.Velocity));
        builder.AddGet("unit.move.isMoving", UnitValueCategory.Bool, (in UnitMoveComponent value) => UnitValue.FromBool(math.lengthsq(value.Velocity) > 0.0001f));

        builder.AddSet("unit.move.setDirection", UnitValueCategory.Float2,
            (ref UnitMoveComponent value, UnitValue input) =>
            {
                value.Direction = input.Float2;
                return true;
            });
        builder.AddSet("unit.move.setVelocity", UnitValueCategory.Float2,
            (ref UnitMoveComponent value, UnitValue input) =>
            {
                value.Velocity = input.Float2;
                return true;
            });
        builder.AddSet("unit.move.setStateMoveMultiplier", UnitValueCategory.Number,
            (ref UnitMoveComponent value, UnitValue input) =>
            {
                if (!input.TryGetNumber(out float multiplier))
                    return false;

                value.StateMoveMultiplier = multiplier;
                return true;
            });
    }
}
