using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

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
            Entity unit = units[i].Value;
            if (MatchesFaction(entityManager, unit, faction))
                destination.Add(unit);
        }
    }

    public static int GetCountByFaction(EntityManager entityManager, Entity observer, UnitFactionType faction)
    {
        if (!TryGetPerceivedUnits(entityManager, observer, out DynamicBuffer<UnitPerceptionUnitElement> units))
            return 0;

        int count = 0;
        for (int i = 0; i < units.Length; i++)
        {
            if (MatchesFaction(entityManager, units[i].Value, faction))
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
            Entity unit = units[i].Value;
            if (!MatchesFaction(entityManager, unit, faction))
                continue;

            if (currentIndex++ == index)
                return unit;
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
        return TryGetNearest(
            entityManager,
            observer,
            candidate => MatchesFaction(entityManager, candidate, faction),
            out unit,
            out distance);
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
            Entity unit = units[i].Value;
            if (MatchesUnitName(entityManager, unit, name))
                destination.Add(unit);
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
            if (MatchesUnitName(entityManager, units[i].Value, name))
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
            Entity unit = units[i].Value;
            if (!MatchesUnitName(entityManager, unit, name))
                continue;

            if (currentIndex++ == index)
                return unit;
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
        return TryGetNearest(
            entityManager,
            observer,
            candidate => MatchesUnitName(entityManager, candidate, name),
            out unit,
            out distance);
    }

    private static bool TryGetNearest(
        EntityManager entityManager,
        Entity observer,
        Func<Entity, bool> predicate,
        out Entity unit,
        out float distance)
    {
        unit = Entity.Null;
        distance = 0f;
        if (predicate == null ||
            !TryGetPerceivedUnits(entityManager, observer, out DynamicBuffer<UnitPerceptionUnitElement> units) ||
            !entityManager.HasComponent<LocalTransform>(observer))
        {
            return false;
        }

        float3 observerPosition = entityManager.GetComponentData<LocalTransform>(observer).Position;
        float nearestDistanceSq = float.MaxValue;
        for (int i = 0; i < units.Length; i++)
        {
            Entity candidate = units[i].Value;
            if (!predicate(candidate) || !entityManager.HasComponent<LocalTransform>(candidate))
                continue;

            float distanceSq = math.distancesq(
                observerPosition,
                entityManager.GetComponentData<LocalTransform>(candidate).Position);
            if (distanceSq >= nearestDistanceSq)
                continue;

            nearestDistanceSq = distanceSq;
            unit = candidate;
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

    private static bool MatchesFaction(EntityManager entityManager, Entity unit, UnitFactionType faction)
    {
        return IsValidPerceivedUnit(entityManager, unit) &&
               entityManager.GetComponentData<UnitFactionComponent>(unit).Value == faction;
    }

    private static bool MatchesUnitName(EntityManager entityManager, Entity unit, in FixedString128Bytes unitName)
    {
        return IsValidPerceivedUnit(entityManager, unit) &&
               entityManager.HasComponent<UnitPerceptionComponent>(unit) &&
               entityManager.GetComponentData<UnitPerceptionComponent>(unit).UnitName.Equals(unitName);
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
