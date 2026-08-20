using CrystalMagic.Game.Data;
using Unity.Entities;
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
            });
            AddBuffer<UnitPerceptionEntityElement>(entity);
        }
    }
}

public struct UnitPerceptionComponent : IComponentData
{
    public float SearchRadius;
}

public struct UnitPerceptionEntityElement : IBufferElementData
{
    public Entity Value;
}

[UnitSourceAuthoring(typeof(UnitPerceptionAuthoring))]
public sealed class UnitPerceptionSource : UnitComponentSource
{
    private static readonly ComparatorParameterDefinition[] s_indexParameter =
    {
        new ComparatorParameterDefinition("Index", UnitValueCategory.Number),
    };

    public override System.Type ComponentType => typeof(UnitPerceptionComponent);

    public override void Describe(UnitSourceSchemaBuilder schema)
    {
        schema.AddGet("unit.perception.searchRadius", ComponentType, UnitValueCategory.Number, System.Array.Empty<ComparatorParameterDefinition>());
        schema.AddGet("unit.perception.entityCount", ComponentType, UnitValueCategory.Number, System.Array.Empty<ComparatorParameterDefinition>());
        schema.AddGet("unit.perception.entityAt", ComponentType, UnitValueCategory.Entity, s_indexParameter);
    }

    public override void Bind(in UnitSourceBindingContext context, UnitSourceAccessTable table)
    {
        EntityManager entityManager = context.EntityManager;
        Entity entity = context.Entity;
        if (!entityManager.Exists(entity) ||
            !entityManager.HasComponent<UnitPerceptionComponent>(entity) ||
            !entityManager.HasBuffer<UnitPerceptionEntityElement>(entity))
        {
            return;
        }

        table.AddGet(new UnitSourceGet(
            "unit.perception.searchRadius",
            UnitValueCategory.Number,
            System.Array.Empty<ComparatorParameterDefinition>(),
            _ => entityManager.Exists(entity) && entityManager.HasComponent<UnitPerceptionComponent>(entity)
                ? UnitValue.FromFloat(entityManager.GetComponentData<UnitPerceptionComponent>(entity).SearchRadius)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "unit.perception.entityCount",
            UnitValueCategory.Number,
            System.Array.Empty<ComparatorParameterDefinition>(),
            _ => entityManager.Exists(entity) && entityManager.HasBuffer<UnitPerceptionEntityElement>(entity)
                ? UnitValue.FromInt(entityManager.GetBuffer<UnitPerceptionEntityElement>(entity).Length)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "unit.perception.entityAt",
            UnitValueCategory.Entity,
            s_indexParameter,
            input => entityManager.Exists(entity) &&
                     entityManager.HasBuffer<UnitPerceptionEntityElement>(entity) &&
                     TryGetIndex(input, out int index) &&
                     index >= 0 &&
                     index < entityManager.GetBuffer<UnitPerceptionEntityElement>(entity).Length
                ? UnitValue.FromEntity(entityManager.GetBuffer<UnitPerceptionEntityElement>(entity)[index].Value)
                : UnitValue.None));
    }

    private static bool TryGetIndex(UnitValue[] input, out int index)
    {
        index = 0;
        if (input == null || input.Length != 1 || !input[0].TryGetNumber(out float value))
            return false;

        index = Mathf.RoundToInt(value);
        return Mathf.Abs(value - index) <= 0.0001f;
    }
}
