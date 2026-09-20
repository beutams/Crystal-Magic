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
        Entity entity,
        in ComponentLookup<UnitPerceptionComponent> perceptions,
        in BufferLookup<UnitPerceptionUnitElement> perceptionUnits,
        in ComponentLookup<UnitFactionComponent> factions,
        in ComponentLookup<UnitDeathComponent> deaths,
        in ComponentLookup<DestroyEntityFlag> destroyFlags,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = default;
        if (!perceptions.TryGetComponent(entity, out UnitPerceptionComponent perception) ||
            !perceptionUnits.TryGetBuffer(entity, out DynamicBuffer<UnitPerceptionUnitElement> units))
        {
            return false;
        }

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
                result = UnitSourceValue.FromInt(GetCountByFaction(
                    in units,
                    faction,
                    in factions,
                    in deaths,
                    in destroyFlags));
                return true;
            case 5 when TryGetFaction(in arguments, 0, out UnitFactionType faction) &&
                             arguments.TryGetInt(1, out int index):
                result = UnitSourceValue.FromEntity(GetByFactionAt(
                    in units,
                    faction,
                    index,
                    in factions,
                    in deaths,
                    in destroyFlags));
                return true;
            case 6 when TryGetFaction(in arguments, 0, out UnitFactionType faction) &&
                             TryGetNearestByFaction(
                                 in units,
                                 faction,
                                 in factions,
                                 in deaths,
                                 in destroyFlags,
                                 out Entity factionUnit):
                result = UnitSourceValue.FromEntity(factionUnit);
                return true;
            case 7 when arguments.TryGetString(0, out FixedString128Bytes unitName):
                result = UnitSourceValue.FromInt(GetCountByName(
                    in units,
                    in unitName,
                    in perceptions,
                    in factions,
                    in deaths,
                    in destroyFlags));
                return true;
            case 8 when arguments.TryGetString(0, out FixedString128Bytes unitName) &&
                             arguments.TryGetInt(1, out int index):
                result = UnitSourceValue.FromEntity(GetByNameAt(
                    in units,
                    in unitName,
                    index,
                    in perceptions,
                    in factions,
                    in deaths,
                    in destroyFlags));
                return true;
            case 9 when arguments.TryGetString(0, out FixedString128Bytes unitName) &&
                             TryGetNearestByName(
                                 in units,
                                 in unitName,
                                 in perceptions,
                                 in factions,
                                 in deaths,
                                 in destroyFlags,
                                 out Entity nameUnit):
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
            factionValue < (int)UnitFactionType.Player || factionValue > (int)UnitFactionType.Npc)
            return false;

        faction = (UnitFactionType)factionValue;
        return true;
    }

    private static int GetCountByFaction(
        in DynamicBuffer<UnitPerceptionUnitElement> units,
        UnitFactionType faction,
        in ComponentLookup<UnitFactionComponent> factions,
        in ComponentLookup<UnitDeathComponent> deaths,
        in ComponentLookup<DestroyEntityFlag> destroyFlags)
    {
        int count = 0;
        for (int index = 0; index < units.Length; index++)
        {
            UnitPerceptionUnitElement unit = units[index];
            if (unit.Faction == faction && IsAvailable(unit.Value, in factions, in deaths, in destroyFlags))
                count++;
        }

        return count;
    }

    private static Entity GetByFactionAt(
        in DynamicBuffer<UnitPerceptionUnitElement> units,
        UnitFactionType faction,
        int targetIndex,
        in ComponentLookup<UnitFactionComponent> factions,
        in ComponentLookup<UnitDeathComponent> deaths,
        in ComponentLookup<DestroyEntityFlag> destroyFlags)
    {
        if (targetIndex < 0)
            return Entity.Null;

        int currentIndex = 0;
        for (int index = 0; index < units.Length; index++)
        {
            UnitPerceptionUnitElement unit = units[index];
            if (unit.Faction != faction || !IsAvailable(unit.Value, in factions, in deaths, in destroyFlags))
                continue;

            if (currentIndex++ == targetIndex)
                return unit.Value;
        }

        return Entity.Null;
    }

    private static bool TryGetNearestByFaction(
        in DynamicBuffer<UnitPerceptionUnitElement> units,
        UnitFactionType faction,
        in ComponentLookup<UnitFactionComponent> factions,
        in ComponentLookup<UnitDeathComponent> deaths,
        in ComponentLookup<DestroyEntityFlag> destroyFlags,
        out Entity result)
    {
        result = Entity.Null;
        float nearestDistanceSq = float.MaxValue;
        for (int index = 0; index < units.Length; index++)
        {
            UnitPerceptionUnitElement unit = units[index];
            if (unit.Faction != faction || unit.DistanceSq >= nearestDistanceSq ||
                !IsAvailable(unit.Value, in factions, in deaths, in destroyFlags))
            {
                continue;
            }

            nearestDistanceSq = unit.DistanceSq;
            result = unit.Value;
        }

        return result != Entity.Null;
    }

    private static int GetCountByName(
        in DynamicBuffer<UnitPerceptionUnitElement> units,
        in FixedString128Bytes unitName,
        in ComponentLookup<UnitPerceptionComponent> perceptions,
        in ComponentLookup<UnitFactionComponent> factions,
        in ComponentLookup<UnitDeathComponent> deaths,
        in ComponentLookup<DestroyEntityFlag> destroyFlags)
    {
        int count = 0;
        for (int index = 0; index < units.Length; index++)
        {
            if (MatchesName(
                    units[index].Value,
                    in unitName,
                    in perceptions,
                    in factions,
                    in deaths,
                    in destroyFlags))
            {
                count++;
            }
        }

        return count;
    }

    private static Entity GetByNameAt(
        in DynamicBuffer<UnitPerceptionUnitElement> units,
        in FixedString128Bytes unitName,
        int targetIndex,
        in ComponentLookup<UnitPerceptionComponent> perceptions,
        in ComponentLookup<UnitFactionComponent> factions,
        in ComponentLookup<UnitDeathComponent> deaths,
        in ComponentLookup<DestroyEntityFlag> destroyFlags)
    {
        if (targetIndex < 0)
            return Entity.Null;

        int currentIndex = 0;
        for (int index = 0; index < units.Length; index++)
        {
            Entity candidate = units[index].Value;
            if (!MatchesName(
                    candidate,
                    in unitName,
                    in perceptions,
                    in factions,
                    in deaths,
                    in destroyFlags))
            {
                continue;
            }

            if (currentIndex++ == targetIndex)
                return candidate;
        }

        return Entity.Null;
    }

    private static bool TryGetNearestByName(
        in DynamicBuffer<UnitPerceptionUnitElement> units,
        in FixedString128Bytes unitName,
        in ComponentLookup<UnitPerceptionComponent> perceptions,
        in ComponentLookup<UnitFactionComponent> factions,
        in ComponentLookup<UnitDeathComponent> deaths,
        in ComponentLookup<DestroyEntityFlag> destroyFlags,
        out Entity result)
    {
        result = Entity.Null;
        float nearestDistanceSq = float.MaxValue;
        for (int index = 0; index < units.Length; index++)
        {
            UnitPerceptionUnitElement unit = units[index];
            if (unit.DistanceSq >= nearestDistanceSq ||
                !MatchesName(
                    unit.Value,
                    in unitName,
                    in perceptions,
                    in factions,
                    in deaths,
                    in destroyFlags))
            {
                continue;
            }

            nearestDistanceSq = unit.DistanceSq;
            result = unit.Value;
        }

        return result != Entity.Null;
    }

    private static bool MatchesName(
        Entity entity,
        in FixedString128Bytes unitName,
        in ComponentLookup<UnitPerceptionComponent> perceptions,
        in ComponentLookup<UnitFactionComponent> factions,
        in ComponentLookup<UnitDeathComponent> deaths,
        in ComponentLookup<DestroyEntityFlag> destroyFlags)
    {
        return IsAvailable(entity, in factions, in deaths, in destroyFlags) &&
               perceptions.TryGetComponent(entity, out UnitPerceptionComponent perception) &&
               perception.UnitName.Equals(unitName);
    }

    private static bool IsAvailable(
        Entity entity,
        in ComponentLookup<UnitFactionComponent> factions,
        in ComponentLookup<UnitDeathComponent> deaths,
        in ComponentLookup<DestroyEntityFlag> destroyFlags)
    {
        return entity != Entity.Null && factions.HasComponent(entity) &&
               (!deaths.HasComponent(entity) || !deaths.IsComponentEnabled(entity)) &&
               (!destroyFlags.HasComponent(entity) || !destroyFlags.IsComponentEnabled(entity));
    }
}
