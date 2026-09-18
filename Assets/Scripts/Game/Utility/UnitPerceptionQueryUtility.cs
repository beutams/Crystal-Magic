using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Queries the units already discovered by an observer's <see cref="UnitPerceptionComponent"/>.
/// These helpers never perform a new spatial search.
/// </summary>
public static class UnitPerceptionQueryUtility
{
    public static List<Entity> GetByFaction(
        EntityManager entityManager,
        Entity observer,
        UnitFactionType faction)
    {
        List<Entity> units = new();
        GetByFaction(entityManager, observer, faction, units);
        return units;
    }

    public static void GetByFaction(
        EntityManager entityManager,
        Entity observer,
        UnitFactionType faction,
        List<Entity> destination)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));

        destination.Clear();
        if (!TryGetPerceivedUnits(entityManager, observer, out DynamicBuffer<UnitPerceptionUnitElement> units))
            return;

        for (int i = 0; i < units.Length; i++)
        {
            UnitPerceptionUnitElement unit = units[i];
            if (MatchesFaction(entityManager, in unit, faction))
                destination.Add(unit.Value);
        }
    }

    public static int GetCountByFaction(EntityManager entityManager, Entity observer, UnitFactionType faction)
    {
        if (!TryGetPerceivedUnits(entityManager, observer, out DynamicBuffer<UnitPerceptionUnitElement> units))
            return 0;

        int count = 0;
        for (int i = 0; i < units.Length; i++)
        {
            UnitPerceptionUnitElement unit = units[i];
            if (MatchesFaction(entityManager, in unit, faction))
                count++;
        }

        return count;
    }

    public static Entity GetByFactionAt(EntityManager entityManager, Entity observer, UnitFactionType faction, int index)
    {
        if (index < 0 || !TryGetPerceivedUnits(entityManager, observer, out DynamicBuffer<UnitPerceptionUnitElement> units))
            return Entity.Null;

        int currentIndex = 0;
        for (int i = 0; i < units.Length; i++)
        {
            UnitPerceptionUnitElement unit = units[i];
            if (!MatchesFaction(entityManager, in unit, faction))
                continue;

            if (currentIndex++ == index)
                return unit.Value;
        }

        return Entity.Null;
    }

    public static bool TryGetNearestByFaction(
        EntityManager entityManager,
        Entity observer,
        UnitFactionType faction,
        out Entity unit,
        out float distance)
    {
        unit = Entity.Null;
        distance = 0f;
        if (!TryGetPerceivedUnits(entityManager, observer, out DynamicBuffer<UnitPerceptionUnitElement> units))
            return false;

        float nearestDistanceSq = float.MaxValue;
        for (int i = 0; i < units.Length; i++)
        {
            UnitPerceptionUnitElement candidate = units[i];
            if (!MatchesFaction(entityManager, in candidate, faction) ||
                candidate.DistanceSq >= nearestDistanceSq)
            {
                continue;
            }

            nearestDistanceSq = candidate.DistanceSq;
            unit = candidate.Value;
        }

        if (unit == Entity.Null)
            return false;

        distance = math.sqrt(nearestDistanceSq);
        return true;
    }

    public static List<Entity> GetByName(EntityManager entityManager, Entity observer, string unitName)
    {
        List<Entity> units = new();
        GetByName(entityManager, observer, unitName, units);
        return units;
    }

    public static void GetByName(
        EntityManager entityManager,
        Entity observer,
        string unitName,
        List<Entity> destination)
    {
        if (destination == null)
            throw new ArgumentNullException(nameof(destination));

        destination.Clear();
        FixedString128Bytes name = new(unitName ?? string.Empty);
        if (!TryGetPerceivedUnits(entityManager, observer, out DynamicBuffer<UnitPerceptionUnitElement> units))
            return;

        for (int i = 0; i < units.Length; i++)
        {
            UnitPerceptionUnitElement unit = units[i];
            if (MatchesUnitName(entityManager, in unit, name))
                destination.Add(unit.Value);
        }
    }

    public static int GetCountByName(EntityManager entityManager, Entity observer, string unitName)
    {
        FixedString128Bytes name = new(unitName ?? string.Empty);
        if (!TryGetPerceivedUnits(entityManager, observer, out DynamicBuffer<UnitPerceptionUnitElement> units))
            return 0;

        int count = 0;
        for (int i = 0; i < units.Length; i++)
        {
            UnitPerceptionUnitElement unit = units[i];
            if (MatchesUnitName(entityManager, in unit, name))
                count++;
        }

        return count;
    }

    public static Entity GetByNameAt(EntityManager entityManager, Entity observer, string unitName, int index)
    {
        if (index < 0)
            return Entity.Null;

        FixedString128Bytes name = new(unitName ?? string.Empty);
        if (!TryGetPerceivedUnits(entityManager, observer, out DynamicBuffer<UnitPerceptionUnitElement> units))
            return Entity.Null;

        int currentIndex = 0;
        for (int i = 0; i < units.Length; i++)
        {
            UnitPerceptionUnitElement unit = units[i];
            if (!MatchesUnitName(entityManager, in unit, name))
                continue;

            if (currentIndex++ == index)
                return unit.Value;
        }

        return Entity.Null;
    }

    public static bool TryGetNearestByName(
        EntityManager entityManager,
        Entity observer,
        string unitName,
        out Entity unit,
        out float distance)
    {
        FixedString128Bytes name = new(unitName ?? string.Empty);
        unit = Entity.Null;
        distance = 0f;
        if (!TryGetPerceivedUnits(entityManager, observer, out DynamicBuffer<UnitPerceptionUnitElement> units))
            return false;

        float nearestDistanceSq = float.MaxValue;
        for (int i = 0; i < units.Length; i++)
        {
            UnitPerceptionUnitElement candidate = units[i];
            if (!MatchesUnitName(entityManager, in candidate, name) ||
                candidate.DistanceSq >= nearestDistanceSq)
            {
                continue;
            }

            nearestDistanceSq = candidate.DistanceSq;
            unit = candidate.Value;
        }

        if (unit == Entity.Null)
            return false;

        distance = math.sqrt(nearestDistanceSq);
        return true;
    }

    private static bool TryGetPerceivedUnits(
        EntityManager entityManager,
        Entity observer,
        out DynamicBuffer<UnitPerceptionUnitElement> units)
    {
        units = default;
        if (observer == Entity.Null ||
            !entityManager.Exists(observer) ||
            !entityManager.HasComponent<UnitPerceptionComponent>(observer) ||
            !entityManager.HasBuffer<UnitPerceptionUnitElement>(observer))
        {
            return false;
        }

        units = entityManager.GetBuffer<UnitPerceptionUnitElement>(observer);
        return true;
    }

    private static bool MatchesFaction(
        EntityManager entityManager,
        in UnitPerceptionUnitElement unit,
        UnitFactionType faction)
    {
        return unit.Faction == faction && IsValidPerceivedUnit(entityManager, unit.Value);
    }

    private static bool MatchesUnitName(
        EntityManager entityManager,
        in UnitPerceptionUnitElement unit,
        in FixedString128Bytes unitName)
    {
        return IsValidPerceivedUnit(entityManager, unit.Value) &&
               entityManager.HasComponent<UnitPerceptionComponent>(unit.Value) &&
               entityManager.GetComponentData<UnitPerceptionComponent>(unit.Value).UnitName.Equals(unitName);
    }

    private static bool IsValidPerceivedUnit(EntityManager entityManager, Entity unit)
    {
        if (unit == Entity.Null ||
            !entityManager.Exists(unit) ||
            !entityManager.HasComponent<UnitFactionComponent>(unit))
        {
            return false;
        }

        return (!entityManager.HasComponent<DestroyEntityFlag>(unit) ||
                !entityManager.IsComponentEnabled<DestroyEntityFlag>(unit)) &&
               (!entityManager.HasComponent<UnitDeathComponent>(unit) ||
                !entityManager.IsComponentEnabled<UnitDeathComponent>(unit));
    }
}
