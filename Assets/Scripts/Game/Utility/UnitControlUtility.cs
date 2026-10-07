using Unity.Entities;
using Unity.Mathematics;

public static class UnitControlUtility
{
    public static void ApplyKnockback(EntityManager entityManager, Entity target, Entity source, float2 direction, float force, float durationSeconds)
    {
        if (!TryGetRuntime(entityManager, target, out UnitControlRuntimeComponent runtime))
            return;

        float2 normalizedDirection = math.normalizesafe(direction, new float2(1f, 0f));
        float clampedForce = math.max(0f, force);
        float clampedDuration = math.max(0.01f, durationSeconds);
        float2 motionVelocity = normalizedDirection * clampedForce;
        float motionDamping = clampedForce / clampedDuration;

        ApplyOrRefreshControl(
            entityManager,
            target,
            source,
            UnitControlType.Knockback,
            durationSeconds,
            GetPriority(UnitControlType.Knockback),
            lockMove: true,
            lockCast: true,
            interruptOnApply: true,
            motionVelocity,
            motionDamping,
            runtime);
    }

    public static void ApplyStun(EntityManager entityManager, Entity target, Entity source, float durationSeconds)
    {
        if (!TryGetRuntime(entityManager, target, out UnitControlRuntimeComponent runtime))
            return;

        ApplyOrRefreshControl(
            entityManager,
            target,
            source,
            UnitControlType.Stun,
            durationSeconds,
            GetPriority(UnitControlType.Stun),
            lockMove: true,
            lockCast: true,
            interruptOnApply: true,
            float2.zero,
            0f,
            runtime);
    }

    public static void ApplyFear(EntityManager entityManager, Entity target, Entity source, float durationSeconds)
    {
        if (!TryGetRuntime(entityManager, target, out UnitControlRuntimeComponent runtime))
            return;

        ApplyOrRefreshControl(
            entityManager,
            target,
            source,
            UnitControlType.Fear,
            durationSeconds,
            GetPriority(UnitControlType.Fear),
            lockMove: true,
            lockCast: true,
            interruptOnApply: true,
            float2.zero,
            0f,
            runtime);
    }

    public static void RefreshControlState(EntityManager entityManager, Entity entity)
    {
        if (!TryGetRuntime(entityManager, entity, out UnitControlRuntimeComponent runtime))
            return;

        RefreshResolvedState(ref runtime);
        runtime.NetworkDirty = 1;
        entityManager.SetComponentData(entity, runtime);
    }

    public static void TickAndRefresh(ref UnitControlRuntimeComponent runtime, float deltaTime)
    {
        float safeDeltaTime = math.max(0f, deltaTime);
        bool removed = false;
        for (int i = runtime.Immunities.Length - 1; i >= 0; i--)
        {
            UnitControlImmunityEntry immunity = runtime.Immunities[i];
            immunity.RemainingTime = math.max(0f, immunity.RemainingTime - safeDeltaTime);
            if (immunity.RemainingTime <= 0.00001f)
            {
                runtime.Immunities.RemoveAt(i);
                removed = true;
            }
            else
                runtime.Immunities[i] = immunity;
        }
        for (int i = runtime.Entries.Length - 1; i >= 0; i--)
        {
            UnitControlRuntimeEntry entry = runtime.Entries[i];
            entry.RemainingTime = math.max(0f, entry.RemainingTime - safeDeltaTime);
            entry.MotionVelocity = DampenVelocity(entry.MotionVelocity, entry.MotionDamping, safeDeltaTime);

            if (entry.RemainingTime <= 0.00001f)
            {
                runtime.Entries.RemoveAt(i);
                removed = true;
            }
            else
                runtime.Entries[i] = entry;
        }

        RefreshResolvedState(ref runtime);
        if (removed)
            runtime.NetworkDirty = 1;
    }

    public static float GetImmunityRemaining(in UnitControlRuntimeComponent runtime, UnitControlType controlType)
    {
        for (int i = 0; i < runtime.Immunities.Length; i++)
            if (runtime.Immunities[i].ControlType == controlType)
                return runtime.Immunities[i].RemainingTime;
        return 0f;
    }

    public static float GetHardControlImmunityInterval(float duration, float multiplier, float minimumSeconds)
    {
        return math.max(math.max(0f, minimumSeconds), math.max(0f, duration) * math.max(0f, multiplier));
    }

    public static bool IsHardControl(UnitControlType type)
    {
        return type == UnitControlType.Knockback || type == UnitControlType.Stun || type == UnitControlType.Fear;
    }

    public static bool StatesMatch(in UnitControlRuntimeComponent a, in UnitControlRuntimeComponent b)
    {
        const float tolerance = 0.0001f;
        if (a.Entries.Length != b.Entries.Length || a.Immunities.Length != b.Immunities.Length ||
            a.HardControlImmunityEnabled != b.HardControlImmunityEnabled ||
            math.abs(a.HardControlImmunityDurationMultiplier - b.HardControlImmunityDurationMultiplier) > tolerance ||
            math.abs(a.HardControlImmunityMinimumSeconds - b.HardControlImmunityMinimumSeconds) > tolerance)
            return false;
        for (int i = 0; i < a.Entries.Length; i++)
        {
            UnitControlRuntimeEntry left = a.Entries[i];
            bool found = false;
            for (int j = 0; j < b.Entries.Length; j++)
            {
                UnitControlRuntimeEntry right = b.Entries[j];
                if (left.ControlType != right.ControlType)
                    continue;
                found = left.Priority == right.Priority && left.LockMove == right.LockMove &&
                        left.LockCast == right.LockCast && left.InterruptOnApply == right.InterruptOnApply &&
                        left.SourceEntity == right.SourceEntity &&
                        math.abs(left.RemainingTime - right.RemainingTime) <= tolerance &&
                        math.distancesq(left.MotionVelocity, right.MotionVelocity) <= tolerance * tolerance &&
                        math.abs(left.MotionDamping - right.MotionDamping) <= tolerance;
                break;
            }
            if (!found)
                return false;
        }
        for (int i = 0; i < a.Immunities.Length; i++)
            if (math.abs(a.Immunities[i].RemainingTime - GetImmunityRemaining(b, a.Immunities[i].ControlType)) > tolerance)
                return false;
        return true;
    }

    public static void ClearAll(EntityManager entityManager, Entity entity)
    {
        if (!TryGetRuntime(entityManager, entity, out UnitControlRuntimeComponent runtime))
            return;
        if (runtime.Entries.Length == 0 && runtime.Immunities.Length == 0)
            return;
        runtime.Entries.Clear();
        runtime.Immunities.Clear();
        RefreshResolvedState(ref runtime);
        runtime.NetworkDirty = 1;
        entityManager.SetComponentData(entity, runtime);
    }

    public static bool HasActiveControl(EntityManager entityManager, Entity entity)
    {
        if (entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<UnitControlRuntimeComponent>(entity))
        {
            return false;
        }

        UnitControlRuntimeComponent control = entityManager.GetComponentData<UnitControlRuntimeComponent>(entity);
        return control.HasControl != 0;
    }

    private static void ApplyOrRefreshControl(
        EntityManager entityManager,
        Entity target,
        Entity source,
        UnitControlType controlType,
        float durationSeconds,
        int priority,
        bool lockMove,
        bool lockCast,
        bool interruptOnApply,
        float2 motionVelocity,
        float motionDamping,
        UnitControlRuntimeComponent runtime)
    {
        float clampedDuration = math.max(0.01f, durationSeconds);
        bool protectPlayer = runtime.HardControlImmunityEnabled != 0 &&
                             entityManager.HasComponent<PlayerInputComponent>(target) && IsHardControl(controlType);
        if (protectPlayer)
        {
            if (GetImmunityRemaining(runtime, controlType) > 0f)
                return;
            for (int i = 0; i < runtime.Entries.Length; i++)
                if (runtime.Entries[i].ControlType == controlType && runtime.Entries[i].RemainingTime > 0f)
                    return;
        }
        bool found = false;

        for (int i = 0; i < runtime.Entries.Length; i++)
        {
            UnitControlRuntimeEntry entry = runtime.Entries[i];
            if (entry.ControlType != controlType)
                continue;

            entry.RemainingTime = math.max(entry.RemainingTime, clampedDuration);
            entry.Priority = priority;
            entry.LockMove = BoolToByte(lockMove);
            entry.LockCast = BoolToByte(lockCast);
            entry.InterruptOnApply = BoolToByte(interruptOnApply);
            entry.SourceEntity = source;
            entry.MotionVelocity = motionVelocity;
            entry.MotionDamping = math.max(0f, motionDamping);
            runtime.Entries[i] = entry;
            found = true;
            break;
        }

        if (!found)
        {
            runtime.Entries.Add(new UnitControlRuntimeEntry
            {
                ControlType = controlType,
                RemainingTime = clampedDuration,
                Priority = priority,
                LockMove = BoolToByte(lockMove),
                LockCast = BoolToByte(lockCast),
                InterruptOnApply = BoolToByte(interruptOnApply),
                SourceEntity = source,
                MotionVelocity = motionVelocity,
                MotionDamping = math.max(0f, motionDamping),
            });
        }

        if (protectPlayer)
        {
            runtime.Immunities.Add(new UnitControlImmunityEntry
            {
                ControlType = controlType,
                RemainingTime = clampedDuration + GetHardControlImmunityInterval(clampedDuration,
                    runtime.HardControlImmunityDurationMultiplier, runtime.HardControlImmunityMinimumSeconds),
            });
        }

        RefreshResolvedState(ref runtime);
        runtime.NetworkDirty = 1;
        entityManager.SetComponentData(target, runtime);
    }

    private static bool TryGetRuntime(EntityManager entityManager, Entity entity, out UnitControlRuntimeComponent runtime)
    {
        runtime = default;
        if (entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<UnitControlRuntimeComponent>(entity))
        {
            return false;
        }

        runtime = entityManager.GetComponentData<UnitControlRuntimeComponent>(entity);
        return true;
    }

    public static void RefreshResolvedState(ref UnitControlRuntimeComponent runtime)
    {
        int selectedIndex = -1;
        int selectedPriority = int.MinValue;

        for (int i = 0; i < runtime.Entries.Length; i++)
        {
            UnitControlRuntimeEntry entry = runtime.Entries[i];
            if (entry.RemainingTime <= 0f)
                continue;

            if (selectedIndex < 0 || entry.Priority > selectedPriority)
            {
                selectedIndex = i;
                selectedPriority = entry.Priority;
            }
        }

        if (selectedIndex < 0)
        {
            runtime.ActiveType = UnitControlType.None;
            runtime.ActiveRemainingTime = 0f;
            runtime.ActivePriority = 0;
            runtime.LockMove = 0;
            runtime.LockCast = 0;
            runtime.HasControl = 0;
            runtime.ActiveSourceEntity = Entity.Null;
            runtime.ActiveMotionVelocity = float2.zero;
            runtime.ActiveMotionDamping = 0f;
            return;
        }

        UnitControlRuntimeEntry active = runtime.Entries[selectedIndex];
        runtime.ActiveType = active.ControlType;
        runtime.ActiveRemainingTime = active.RemainingTime;
        runtime.ActivePriority = active.Priority;
        runtime.LockMove = active.LockMove;
        runtime.LockCast = active.LockCast;
        runtime.HasControl = 1;
        runtime.ActiveSourceEntity = active.SourceEntity;
        runtime.ActiveMotionVelocity = active.MotionVelocity;
        runtime.ActiveMotionDamping = active.MotionDamping;
    }

    private static float2 DampenVelocity(float2 velocity, float damping, float deltaTime)
    {
        float speed = math.length(velocity);
        if (speed <= 0.0001f)
            return float2.zero;

        float safeDamping = math.max(0f, damping);
        if (safeDamping <= 0f)
            return velocity;

        float decelStep = safeDamping * deltaTime;
        if (decelStep >= speed)
            return float2.zero;

        return velocity - (velocity / speed) * decelStep;
    }

    private static int GetPriority(UnitControlType controlType)
    {
        return controlType switch
        {
            UnitControlType.Knockback => 300,
            UnitControlType.Stun => 200,
            UnitControlType.Fear => 100,
            _ => 0,
        };
    }

    private static byte BoolToByte(bool value)
    {
        return value ? (byte)1 : (byte)0;
    }
}
