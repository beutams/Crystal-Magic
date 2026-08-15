using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class UnitPerceptionAuthoring : MonoBehaviour
{
    class UnitPerceptionBaker : Baker<UnitPerceptionAuthoring>
    {
        public override void Bake(UnitPerceptionAuthoring authoring)
        {
            TextAsset unitDataAsset = UnitAuthoringUtility.GetUnitDataTableAsset();
            if (unitDataAsset != null)
                DependsOn(unitDataAsset);

            float searchRadius = 8f;
            UnitPerceptionModuleData data = UnitAuthoringUtility.ResolveModuleData<UnitPerceptionModuleData>(authoring);
            if (data != null)
                searchRadius = Mathf.Max(0f, data.SearchRadius);

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitPerceptionComponent
            {
                SearchRadius = searchRadius,
                HasTarget = false,
                TargetEntity = Entity.Null,
                TargetPosition = float2.zero,
                TargetDistance = 0f,
            });
        }
    }
}

public struct UnitPerceptionComponent : IComponentData
{
    public float SearchRadius;
    public bool HasTarget;
    public Entity TargetEntity;
    public float2 TargetPosition;
    public float TargetDistance;
}

[UnitSourceAuthoring(typeof(UnitPerceptionAuthoring))]
public sealed class UnitPerceptionSource : UnitComponentSource<UnitPerceptionComponent>
{
    protected override void Define(UnitSourceDefinitionBuilder<UnitPerceptionComponent> builder)
    {
        builder.AddGet("unit.perception.searchRadius", UnitValueCategory.Number, (in UnitPerceptionComponent value) => UnitValue.FromFloat(value.SearchRadius));
        builder.AddGet("unit.perception.hasTarget", UnitValueCategory.Bool, (in UnitPerceptionComponent value) => UnitValue.FromBool(value.HasTarget));
        builder.AddGet("unit.perception.targetEntity", UnitValueCategory.Entity, (in UnitPerceptionComponent value) => UnitValue.FromEntity(value.TargetEntity));
        builder.AddGet("unit.perception.targetPosition", UnitValueCategory.Float2, (in UnitPerceptionComponent value) => UnitValue.FromFloat2(value.TargetPosition));
        builder.AddGet("unit.perception.targetDistance", UnitValueCategory.Number, (in UnitPerceptionComponent value) => UnitValue.FromFloat(value.TargetDistance));
    }
}
