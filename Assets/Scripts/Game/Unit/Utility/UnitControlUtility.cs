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
        entityManager.SetComponentData(entity, runtime);
    }

    public static void TickAndRefresh(EntityManager entityManager, Entity entity, float deltaTime)
    {
        if (!TryGetRuntime(entityManager, entity, out UnitControlRuntimeComponent runtime))
            return;

        float safeDeltaTime = math.max(0f, deltaTime);
        for (int i = runtime.Entries.Length - 1; i >= 0; i--)
        {
            UnitControlRuntimeEntry entry = runtime.Entries[i];
            entry.RemainingTime = math.max(0f, entry.RemainingTime - safeDeltaTime);
            entry.MotionVelocity = DampenVelocity(entry.MotionVelocity, entry.MotionDamping, safeDeltaTime);

            if (entry.RemainingTime <= 0f)
                runtime.Entries.RemoveAt(i);
            else
                runtime.Entries[i] = entry;
        }

        RefreshResolvedState(ref runtime);
        entityManager.SetComponentData(entity, runtime);
    }

    public static bool IsInControlledState(EntityManager entityManager, Entity entity)
    {
        if (entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<UnitStateMachineComponent>(entity))
        {
            return false;
        }

        UnitStateMachineComponent stateMachine = entityManager.GetComponentObject<UnitStateMachineComponent>(entity);
        return stateMachine != null && stateMachine.CurrentStateName == nameof(ControlledState);
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

        if (interruptOnApply && entityManager.HasComponent<UnitCastComponent>(target))
        {
            UnitCastComponent cast = entityManager.GetComponentData<UnitCastComponent>(target);
            cast.ForceInterrupt = true;
            entityManager.SetComponentData(target, cast);
        }

        RefreshResolvedState(ref runtime);
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

    private static void RefreshResolvedState(ref UnitControlRuntimeComponent runtime)
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
