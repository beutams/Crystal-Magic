using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public sealed class NetworkControlStateData : NetworkStateData
{
    public List<NetworkControlEntryStateData> entries = new();

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.TryGetEntity(unitId, out Entity entity))
            return;

        UnitControlRuntimeComponent runtime = new()
        {
            Entries = new FixedList512Bytes<UnitControlRuntimeEntry>(),
            ActiveType = UnitControlType.None,
            ActiveSourceEntity = Entity.Null,
        };
        int activeIndex = -1;
        uint activeEndFrame = 0;
        for (int index = 0; index < entries.Count; index++)
        {
            NetworkControlEntryStateData state = entries[index];
            float remainingTime = context.GetRemainingSeconds(state.endFrame);
            if (remainingTime == 0f)
                continue;

            context.TryGetEntity(state.sourceUnitId, out Entity sourceEntity);
            runtime.Entries.Add(new UnitControlRuntimeEntry
            {
                ControlType = state.controlType,
                RemainingTime = remainingTime,
                Priority = state.priority,
                LockMove = state.lockMove,
                LockCast = state.lockCast,
                InterruptOnApply = state.interruptOnApply,
                SourceEntity = sourceEntity,
                MotionVelocity = new float2(state.motionVelocityX, state.motionVelocityY),
                MotionDamping = state.motionDamping,
            });

            if (activeIndex < 0 || state.priority > runtime.ActivePriority)
            {
                activeIndex = runtime.Entries.Length - 1;
                runtime.ActivePriority = state.priority;
                activeEndFrame = state.endFrame;
            }
        }

        if (activeIndex >= 0)
        {
            UnitControlRuntimeEntry active = runtime.Entries[activeIndex];
            runtime.ActiveType = active.ControlType;
            runtime.ActiveRemainingTime = active.RemainingTime;
            runtime.LockMove = active.LockMove;
            runtime.LockCast = active.LockCast;
            runtime.HasControl = 1;
            runtime.ActiveSourceEntity = active.SourceEntity;
            runtime.ActiveMotionVelocity = active.MotionVelocity;
            runtime.ActiveMotionDamping = active.MotionDamping;
        }

        runtime.NetworkDirty = 0;
        context.SetOrAdd(entity, runtime);

        if (context.IsClient)
            ApplyPresentationState(context, entity, runtime, activeEndFrame);
    }

    private static void ApplyPresentationState(
        NetworkStateApplyContext context,
        Entity entity,
        in UnitControlRuntimeComponent runtime,
        uint activeEndFrame)
    {
        ClientControlPresentationComponent presentation = context.EntityManager
            .HasComponent<ClientControlPresentationComponent>(entity)
            ? context.EntityManager.GetComponentData<ClientControlPresentationComponent>(entity)
            : default;
        bool changed = presentation.Active != runtime.HasControl ||
                       presentation.DisplayType != runtime.ActiveType ||
                       presentation.SourceEntity != runtime.ActiveSourceEntity ||
                       presentation.EndFrame != activeEndFrame;
        presentation.DisplayType = runtime.ActiveType;
        presentation.SourceEntity = runtime.ActiveSourceEntity;
        presentation.EndFrame = activeEndFrame;
        presentation.DisplayRemainingTime = runtime.ActiveRemainingTime;
        presentation.Active = runtime.HasControl;
        if (changed)
            presentation.Revision++;
        context.SetOrAdd(entity, presentation);
    }
}

[Serializable]
public sealed class NetworkControlEntryStateData
{
    public UnitControlType controlType;
    public uint endFrame;
    public int priority;
    public byte lockMove;
    public byte lockCast;
    public byte interruptOnApply;
    public Guid sourceUnitId;
    public float motionVelocityX;
    public float motionVelocityY;
    public float motionDamping;
}
