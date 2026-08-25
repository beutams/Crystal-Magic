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

    public float BaseMoveSpeedValue => BaseMoveSpeed + BaseMoveSpeedOffset;
}

[UnitSourceAuthoring(typeof(UnitMoveAuthoring))]
public sealed class UnitMoveSource : UnitComponentSource<UnitMoveComponent>
{
    private static readonly ComparatorParameterDefinition[] s_noParameters = System.Array.Empty<ComparatorParameterDefinition>();

    protected override void Define(UnitSourceDefinitionBuilder<UnitMoveComponent> builder)
    {
        builder.AddGet("unit.move.baseMoveSpeed", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.BaseMoveSpeedValue));
        builder.AddGet("unit.move.baseMaxAcceleration", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.BaseMaxAcceleration));
        builder.AddContextGet("unit.move.realMoveSpeed", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in UnitMoveComponent _, UnitValue[] _) => UnitValue.FromFloat(UnitModifierResolver.GetMoveSpeed(context.EntityManager, context.Entity)));
        builder.AddContextGet("unit.move.realMaxAcceleration", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in UnitMoveComponent _, UnitValue[] _) => UnitValue.FromFloat(UnitModifierResolver.GetMaxAcceleration(context.EntityManager, context.Entity)));
        builder.AddGet("unit.move.direction", UnitValueCategory.Float2, (in UnitMoveComponent value) => UnitValue.FromFloat2(value.Direction));
        builder.AddGet("unit.move.stateMoveMultiplier", UnitValueCategory.Number, (in UnitMoveComponent value) => UnitValue.FromFloat(value.StateMoveMultiplier));

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
