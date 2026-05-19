using Unity.Entities;
using Unity.Mathematics;

public static class UnitControlUtility
{
    public static void ApplyKnockback(EntityManager entityManager, Entity target, Entity source, float2 direction, float force, float durationSeconds)
    {
        if (!entityManager.Exists(target) || !entityManager.HasBuffer<UnitControlElement>(target))
            return;

        float2 normalizedDirection = math.normalizesafe(direction, new float2(1f, 0f));
        float clampedDuration = math.max(0.01f, durationSeconds);
        UnitKnockbackComponent knockback = entityManager.HasComponent<UnitKnockbackComponent>(target)
            ? entityManager.GetComponentData<UnitKnockbackComponent>(target)
            : default;
        knockback.Velocity = normalizedDirection * math.max(0f, force);
        knockback.Damping = math.max(0f, force) / clampedDuration;
        if (entityManager.HasComponent<UnitKnockbackComponent>(target))
            entityManager.SetComponentData(target, knockback);

        ApplyOrRefreshControl(
            entityManager,
            target,
            source,
            UnitControlType.Knockback,
            durationSeconds,
            GetPriority(UnitControlType.Knockback),
            lockMove: true,
            lockCast: true,
            interruptOnApply: true);
    }

    public static void ApplyStun(EntityManager entityManager, Entity target, Entity source, float durationSeconds)
    {
        ApplyOrRefreshControl(
            entityManager,
            target,
            source,
            UnitControlType.Stun,
            durationSeconds,
            GetPriority(UnitControlType.Stun),
            lockMove: true,
            lockCast: true,
            interruptOnApply: true);
    }

    public static void ApplyFear(EntityManager entityManager, Entity target, Entity source, float durationSeconds)
    {
        ApplyOrRefreshControl(
            entityManager,
            target,
            source,
            UnitControlType.Fear,
            durationSeconds,
            GetPriority(UnitControlType.Fear),
            lockMove: true,
            lockCast: true,
            interruptOnApply: true);
    }

    public static void RefreshControlState(EntityManager entityManager, Entity entity)
    {
        if (!entityManager.Exists(entity) ||
            !entityManager.HasBuffer<UnitControlElement>(entity) ||
            !entityManager.HasComponent<UnitControlStateComponent>(entity))
        {
            return;
        }

        DynamicBuffer<UnitControlElement> controls = entityManager.GetBuffer<UnitControlElement>(entity);
        UnitControlStateComponent state = entityManager.GetComponentData<UnitControlStateComponent>(entity);

        int selectedIndex = -1;
        int selectedPriority = int.MinValue;
        for (int i = 0; i < controls.Length; i++)
        {
            UnitControlElement control = controls[i];
            if (control.RemainingTime <= 0f)
                continue;

            if (selectedIndex < 0 || control.Priority > selectedPriority)
            {
                selectedIndex = i;
                selectedPriority = control.Priority;
            }
        }

        if (selectedIndex < 0)
        {
            state.ActiveType = UnitControlType.None;
            state.RemainingTime = 0f;
            state.ActivePriority = 0;
            state.LockMove = 0;
            state.LockCast = 0;
            state.HasControl = 0;
            state.ActiveSourceEntity = Entity.Null;
            entityManager.SetComponentData(entity, state);

            if (entityManager.HasComponent<UnitKnockbackComponent>(entity))
            {
                UnitKnockbackComponent knockback = entityManager.GetComponentData<UnitKnockbackComponent>(entity);
                knockback.Velocity = float2.zero;
                knockback.Damping = 0f;
                entityManager.SetComponentData(entity, knockback);
            }

            return;
        }

        UnitControlElement active = controls[selectedIndex];
        state.ActiveType = active.ControlType;
        state.RemainingTime = active.RemainingTime;
        state.ActivePriority = active.Priority;
        state.LockMove = active.LockMove;
        state.LockCast = active.LockCast;
        state.HasControl = 1;
        state.ActiveSourceEntity = active.SourceEntity;
        entityManager.SetComponentData(entity, state);
    }

    public static void TickAndRefresh(EntityManager entityManager, Entity entity, float deltaTime)
    {
        if (!entityManager.Exists(entity) || !entityManager.HasBuffer<UnitControlElement>(entity))
            return;

        DynamicBuffer<UnitControlElement> controls = entityManager.GetBuffer<UnitControlElement>(entity);
        for (int i = controls.Length - 1; i >= 0; i--)
        {
            UnitControlElement control = controls[i];
            control.RemainingTime = math.max(0f, control.RemainingTime - math.max(0f, deltaTime));
            if (control.RemainingTime <= 0f)
                controls.RemoveAt(i);
            else
                controls[i] = control;
        }

        RefreshControlState(entityManager, entity);
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
        bool interruptOnApply)
    {
        if (!entityManager.Exists(target) || !entityManager.HasBuffer<UnitControlElement>(target))
            return;

        DynamicBuffer<UnitControlElement> controls = entityManager.GetBuffer<UnitControlElement>(target);
        float clampedDuration = math.max(0.01f, durationSeconds);
        bool found = false;

        for (int i = 0; i < controls.Length; i++)
        {
            UnitControlElement control = controls[i];
            if (control.ControlType != controlType)
                continue;

            control.RemainingTime = math.max(control.RemainingTime, clampedDuration);
            control.Priority = priority;
            control.LockMove = BoolToByte(lockMove);
            control.LockCast = BoolToByte(lockCast);
            control.InterruptOnApply = BoolToByte(interruptOnApply);
            control.SourceEntity = source;
            controls[i] = control;
            found = true;
            break;
        }

        if (!found)
        {
            controls.Add(new UnitControlElement
            {
                ControlType = controlType,
                RemainingTime = clampedDuration,
                Priority = priority,
                LockMove = BoolToByte(lockMove),
                LockCast = BoolToByte(lockCast),
                InterruptOnApply = BoolToByte(interruptOnApply),
                SourceEntity = source,
            });
        }

        if (interruptOnApply && entityManager.HasComponent<UnitCastComponent>(target))
        {
            UnitCastComponent cast = entityManager.GetComponentData<UnitCastComponent>(target);
            cast.ForceInterrupt = true;
            entityManager.SetComponentData(target, cast);
        }

        if (entityManager.HasComponent<UnitIntentComponent>(target))
        {
            UnitIntentComponent intent = entityManager.GetComponentData<UnitIntentComponent>(target);
            intent.MoveDirection = float2.zero;
            intent.WantToCast = false;
            entityManager.SetComponentData(target, intent);
        }

        if (entityManager.HasComponent<UnitMoveComponent>(target))
        {
            UnitMoveComponent move = entityManager.GetComponentData<UnitMoveComponent>(target);
            move.AccelInput = float2.zero;
            entityManager.SetComponentData(target, move);
        }

        RefreshControlState(entityManager, target);
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
