using CrystalMagic.Game.Data;
using Unity.Collections;
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

            UnitData unitData = UnitAuthoringUtility.ResolveUnitData(authoring);
            float searchRadius = 8f;
            UnitPerceptionModuleData data = UnitAuthoringUtility.ResolveModuleData<UnitPerceptionModuleData>(authoring);
            if (data != null)
                searchRadius = Mathf.Max(0f, data.SearchRadius);

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitPerceptionComponent
            {
                SearchRadius = searchRadius,
                UnitName = new FixedString128Bytes(unitData?.Name ?? string.Empty),
            });
            AddBuffer<UnitPerceptionUnitElement>(entity);
        }
    }
}

public struct UnitPerceptionComponent : IComponentData
{
    public float SearchRadius;
    public FixedString128Bytes UnitName;
}

public struct UnitPerceptionUnitElement : IBufferElementData
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

    private static readonly ComparatorParameterDefinition[] s_factionParameter =
    {
        new ComparatorParameterDefinition("Faction", UnitValueCategory.Number),
    };

    private static readonly ComparatorParameterDefinition[] s_factionAndIndexParameters =
    {
        new ComparatorParameterDefinition("Faction", UnitValueCategory.Number),
        new ComparatorParameterDefinition("Index", UnitValueCategory.Number),
    };

    private static readonly ComparatorParameterDefinition[] s_unitNameParameter =
    {
        new ComparatorParameterDefinition("Unit Name", UnitValueCategory.String),
    };

    private static readonly ComparatorParameterDefinition[] s_unitNameAndIndexParameters =
    {
        new ComparatorParameterDefinition("Unit Name", UnitValueCategory.String),
        new ComparatorParameterDefinition("Index", UnitValueCategory.Number),
    };

    public override System.Type ComponentType => typeof(UnitPerceptionComponent);

    public override void Describe(UnitSourceSchemaBuilder schema)
    {
        schema.AddGet("unit.perception.searchRadius", ComponentType, UnitValueCategory.Number, System.Array.Empty<ComparatorParameterDefinition>());
        schema.AddGet("unit.perception.unitName", ComponentType, UnitValueCategory.String, System.Array.Empty<ComparatorParameterDefinition>());
        schema.AddGet("unit.perception.unitCount", ComponentType, UnitValueCategory.Number, System.Array.Empty<ComparatorParameterDefinition>());
        schema.AddGet("unit.perception.unitAt", ComponentType, UnitValueCategory.Entity, s_indexParameter);
        schema.AddGet("unit.perception.unitsByFactionCount", ComponentType, UnitValueCategory.Number, s_factionParameter);
        schema.AddGet("unit.perception.unitByFactionAt", ComponentType, UnitValueCategory.Entity, s_factionAndIndexParameters);
        schema.AddGet("unit.perception.nearestUnitByFaction", ComponentType, UnitValueCategory.Entity, s_factionParameter);
        schema.AddGet("unit.perception.unitsByNameCount", ComponentType, UnitValueCategory.Number, s_unitNameParameter);
        schema.AddGet("unit.perception.unitByNameAt", ComponentType, UnitValueCategory.Entity, s_unitNameAndIndexParameters);
        schema.AddGet("unit.perception.nearestUnitByName", ComponentType, UnitValueCategory.Entity, s_unitNameParameter);
    }

    public override void Bind(in UnitSourceBindingContext context, UnitSourceAccessTable table)
    {
        EntityManager entityManager = context.EntityManager;
        Entity entity = context.Entity;
        if (!entityManager.Exists(entity) ||
            !entityManager.HasComponent<UnitPerceptionComponent>(entity) ||
            !entityManager.HasBuffer<UnitPerceptionUnitElement>(entity))
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
            "unit.perception.unitName",
            UnitValueCategory.String,
            System.Array.Empty<ComparatorParameterDefinition>(),
            _ => entityManager.Exists(entity) && entityManager.HasComponent<UnitPerceptionComponent>(entity)
                ? UnitValue.FromString(entityManager.GetComponentData<UnitPerceptionComponent>(entity).UnitName.ToString())
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "unit.perception.unitCount",
            UnitValueCategory.Number,
            System.Array.Empty<ComparatorParameterDefinition>(),
            _ => entityManager.Exists(entity) && entityManager.HasBuffer<UnitPerceptionUnitElement>(entity)
                ? UnitValue.FromInt(entityManager.GetBuffer<UnitPerceptionUnitElement>(entity).Length)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "unit.perception.unitAt",
            UnitValueCategory.Entity,
            s_indexParameter,
            input => entityManager.Exists(entity) &&
                     entityManager.HasBuffer<UnitPerceptionUnitElement>(entity) &&
                     TryGetIndex(input, out int index) &&
                     index >= 0 &&
                     index < entityManager.GetBuffer<UnitPerceptionUnitElement>(entity).Length
                ? UnitValue.FromEntity(entityManager.GetBuffer<UnitPerceptionUnitElement>(entity)[index].Value)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "unit.perception.unitsByFactionCount",
            UnitValueCategory.Number,
            s_factionParameter,
            input => TryGetFaction(input, out UnitFactionType faction)
                ? UnitValue.FromInt(UnitPerceptionQueryUtility.GetCountByFaction(entityManager, entity, faction))
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "unit.perception.unitByFactionAt",
            UnitValueCategory.Entity,
            s_factionAndIndexParameters,
            input => TryGetFactionAndIndex(input, out UnitFactionType faction, out int index)
                ? UnitValue.FromEntity(UnitPerceptionQueryUtility.GetByFactionAt(entityManager, entity, faction, index))
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "unit.perception.nearestUnitByFaction",
            UnitValueCategory.Entity,
            s_factionParameter,
            input => TryGetFaction(input, out UnitFactionType faction) &&
                     UnitPerceptionQueryUtility.TryGetNearestByFaction(entityManager, entity, faction, out Entity unit, out _)
                ? UnitValue.FromEntity(unit)
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "unit.perception.unitsByNameCount",
            UnitValueCategory.Number,
            s_unitNameParameter,
            input => TryGetUnitName(input, out string unitName)
                ? UnitValue.FromInt(UnitPerceptionQueryUtility.GetCountByName(entityManager, entity, unitName))
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "unit.perception.unitByNameAt",
            UnitValueCategory.Entity,
            s_unitNameAndIndexParameters,
            input => TryGetUnitNameAndIndex(input, out string unitName, out int index)
                ? UnitValue.FromEntity(UnitPerceptionQueryUtility.GetByNameAt(entityManager, entity, unitName, index))
                : UnitValue.None));
        table.AddGet(new UnitSourceGet(
            "unit.perception.nearestUnitByName",
            UnitValueCategory.Entity,
            s_unitNameParameter,
            input => TryGetUnitName(input, out string unitName) &&
                     UnitPerceptionQueryUtility.TryGetNearestByName(entityManager, entity, unitName, out Entity unit, out _)
                ? UnitValue.FromEntity(unit)
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

    private static bool TryGetFaction(UnitValue[] input, out UnitFactionType faction)
    {
        faction = default;
        if (!TryGetIndex(input, out int factionValue) ||
            !System.Enum.IsDefined(typeof(UnitFactionType), factionValue))
        {
            return false;
        }

        faction = (UnitFactionType)factionValue;
        return true;
    }

    private static bool TryGetFactionAndIndex(UnitValue[] input, out UnitFactionType faction, out int index)
    {
        faction = default;
        index = 0;
        if (input == null || input.Length != 2)
            return false;

        return TryGetFaction(new[] { input[0] }, out faction) &&
               TryGetIndex(new[] { input[1] }, out index);
    }

    private static bool TryGetUnitName(UnitValue[] input, out string unitName)
    {
        unitName = string.Empty;
        return input != null && input.Length == 1 && input[0].TryGetString(out unitName);
    }

    private static bool TryGetUnitNameAndIndex(UnitValue[] input, out string unitName, out int index)
    {
        unitName = string.Empty;
        index = 0;
        if (input == null || input.Length != 2)
            return false;

        return input[0].TryGetString(out unitName) &&
               TryGetIndex(new[] { input[1] }, out index);
    }
}
