using Unity.Entities;
using Unity.Transforms;

/// <summary>Damage-driven awareness shared only with the victim's actual squad.</summary>
public static class UnitDamageAggroUtility
{
    public const double DurationSeconds = 8;

    public static void NotifyDamage(EntityManager manager, Entity victim, Entity attacker)
    {
        if (!IsMonster(manager, victim) || !IsLivePlayer(manager, attacker))
            return;

        double until = manager.World.Time.ElapsedTime + DurationSeconds;
        Remember(manager, victim, attacker, until);

        Entity owner = GetOwner(manager, victim);
        if (owner == Entity.Null || !manager.Exists(owner) ||
            !manager.HasBuffer<UnitVariableConsumerElement>(owner))
            return;

        // Guards and travelling patrols can share a point owner, but are different squads.
        bool patrol = IsPatrol(manager, victim);
        DynamicBuffer<UnitVariableConsumerElement> members = manager.GetBuffer<UnitVariableConsumerElement>(owner, true);
        for (int i = 0; i < members.Length; i++)
        {
            Entity member = members[i].Value;
            if (member != victim && IsMonster(manager, member) &&
                GetOwner(manager, member) == owner && IsPatrol(manager, member) == patrol)
                Remember(manager, member, attacker, until);
        }
    }

    public static bool HasActiveTarget(EntityManager manager, Entity monster, double now)
    {
        if (!manager.HasComponent<UnitPerceptionComponent>(monster)) return false;
        UnitPerceptionComponent perception = manager.GetComponentData<UnitPerceptionComponent>(monster);
        return perception.HasDamageTarget(now) && IsLivePlayer(manager, perception.DamageTarget);
    }

    private static void Remember(EntityManager manager, Entity monster, Entity attacker, double until)
    {
        if (!manager.HasComponent<UnitPerceptionComponent>(monster) || IsDead(manager, monster)) return;
        UnitPerceptionComponent perception = manager.GetComponentData<UnitPerceptionComponent>(monster);
        perception.DamageTarget = attacker;
        perception.DamageTargetUntil = until;
        manager.SetComponentData(monster, perception);
        // No structural changes here: sleeping squad members wake in the distance system.
    }

    private static Entity GetOwner(EntityManager manager, Entity entity) =>
        manager.HasComponent<UnitOwnerComponent>(entity)
            ? manager.GetComponentData<UnitOwnerComponent>(entity).Owner : Entity.Null;

    private static bool IsPatrol(EntityManager manager, Entity entity) =>
        UnitVariableSource.TryGetValue(manager, entity, DungeonPatrolRuntimeUtility.PatrolMemberKey,
            out UnitSourceValue value) && value.TryGetBool(out bool patrol) && patrol;

    private static bool IsMonster(EntityManager manager, Entity entity) =>
        manager.Exists(entity) && manager.HasComponent<UnitFactionComponent>(entity) &&
        UnitFactionUtility.IsHostile(manager.GetComponentData<UnitFactionComponent>(entity).Value);

    private static bool IsLivePlayer(EntityManager manager, Entity entity) =>
        manager.Exists(entity) && manager.HasComponent<LocalTransform>(entity) &&
        manager.HasComponent<UnitFactionComponent>(entity) &&
        manager.GetComponentData<UnitFactionComponent>(entity).Value == UnitFactionType.Player &&
        !manager.HasComponent<BattleSpectatorComponent>(entity) && !manager.HasComponent<Disabled>(entity) &&
        !IsDead(manager, entity);

    private static bool IsDead(EntityManager manager, Entity entity) =>
        (manager.HasComponent<UnitDeathComponent>(entity) && manager.IsComponentEnabled<UnitDeathComponent>(entity)) ||
        (manager.HasComponent<DestroyEntityFlag>(entity) && manager.IsComponentEnabled<DestroyEntityFlag>(entity)) ||
        (manager.HasComponent<UnitVitalityComponent>(entity) &&
         manager.GetComponentData<UnitVitalityComponent>(entity).CurrentHealth <= 0f);
}
