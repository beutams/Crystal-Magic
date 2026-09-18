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
    public float DistanceSq;
    public UnitFactionType Faction;
}

[UnitSourceProvider(typeof(UnitPerceptionComponent), typeof(UnitPerceptionAuthoring))]
public static class UnitPerceptionSource
{
    [UnitSourceGet(0, "unit.perception.searchRadius", UnitValueCategory.Number)]
    [UnitSourceGet(1, "unit.perception.unitName", UnitValueCategory.String)]
    [UnitSourceGet(2, "unit.perception.unitCount", UnitValueCategory.Number)]
    [UnitSourceGet(3, "unit.perception.unitAt", UnitValueCategory.Entity, UnitValueCategory.Number, ParameterNames = new[] { "Index" })]
    [UnitSourceGet(4, "unit.perception.unitsByFactionCount", UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Faction" })]
    [UnitSourceGet(5, "unit.perception.unitByFactionAt", UnitValueCategory.Entity, UnitValueCategory.Number, UnitValueCategory.Number, ParameterNames = new[] { "Faction", "Index" })]
    [UnitSourceGet(6, "unit.perception.nearestUnitByFaction", UnitValueCategory.Entity, UnitValueCategory.Number, ParameterNames = new[] { "Faction" })]
    [UnitSourceGet(7, "unit.perception.unitsByNameCount", UnitValueCategory.Number, UnitValueCategory.String, ParameterNames = new[] { "Unit Name" })]
    [UnitSourceGet(8, "unit.perception.unitByNameAt", UnitValueCategory.Entity, UnitValueCategory.String, UnitValueCategory.Number, ParameterNames = new[] { "Unit Name", "Index" })]
    [UnitSourceGet(9, "unit.perception.nearestUnitByName", UnitValueCategory.Entity, UnitValueCategory.String, ParameterNames = new[] { "Unit Name" })]
    public static bool TryGet(
        int operation,
        EntityManager entityManager,
        Entity entity,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = default;
        if (!entityManager.Exists(entity) ||
            !entityManager.HasComponent<UnitPerceptionComponent>(entity) ||
            !entityManager.HasBuffer<UnitPerceptionUnitElement>(entity))
        {
            return false;
        }

        UnitPerceptionComponent perception = entityManager.GetComponentData<UnitPerceptionComponent>(entity);
        DynamicBuffer<UnitPerceptionUnitElement> units = entityManager.GetBuffer<UnitPerceptionUnitElement>(entity);
        switch (operation)
        {
            case 0:
                result = UnitSourceValue.FromFloat(perception.SearchRadius);
                return true;
            case 1:
                result = UnitSourceValue.FromString(perception.UnitName);
                return true;
            case 2:
                result = UnitSourceValue.FromInt(units.Length);
                return true;
            case 3 when arguments.TryGetInt(0, out int index) && index >= 0 && index < units.Length:
                result = UnitSourceValue.FromEntity(units[index].Value);
                return true;
            case 4 when TryGetFaction(in arguments, 0, out UnitFactionType faction):
                result = UnitSourceValue.FromInt(UnitPerceptionQueryUtility.GetCountByFaction(entityManager, entity, faction));
                return true;
            case 5 when TryGetFaction(in arguments, 0, out UnitFactionType faction) && arguments.TryGetInt(1, out int index):
                result = UnitSourceValue.FromEntity(UnitPerceptionQueryUtility.GetByFactionAt(entityManager, entity, faction, index));
                return true;
            case 6 when TryGetFaction(in arguments, 0, out UnitFactionType faction) &&
                             UnitPerceptionQueryUtility.TryGetNearestByFaction(entityManager, entity, faction, out Entity factionUnit, out _):
                result = UnitSourceValue.FromEntity(factionUnit);
                return true;
            case 7 when arguments.TryGetString(0, out FixedString128Bytes unitName):
                result = UnitSourceValue.FromInt(UnitPerceptionQueryUtility.GetCountByName(entityManager, entity, unitName.ToString()));
                return true;
            case 8 when arguments.TryGetString(0, out FixedString128Bytes unitName) && arguments.TryGetInt(1, out int index):
                result = UnitSourceValue.FromEntity(UnitPerceptionQueryUtility.GetByNameAt(entityManager, entity, unitName.ToString(), index));
                return true;
            case 9 when arguments.TryGetString(0, out FixedString128Bytes unitName) &&
                             UnitPerceptionQueryUtility.TryGetNearestByName(entityManager, entity, unitName.ToString(), out Entity nameUnit, out _):
                result = UnitSourceValue.FromEntity(nameUnit);
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetFaction(in UnitSourceArguments arguments, int index, out UnitFactionType faction)
    {
        faction = default;
        if (!arguments.TryGetInt(index, out int factionValue) ||
            !System.Enum.IsDefined(typeof(UnitFactionType), factionValue))
        {
            return false;
        }

        faction = (UnitFactionType)factionValue;
        return true;
    }
}
