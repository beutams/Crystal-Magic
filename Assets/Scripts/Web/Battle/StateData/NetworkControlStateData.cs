using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Server;

[Serializable]
public sealed class NetworkControlStateData : NetworkStateData
{
    public List<NetworkControlEntryStateData> entries = new();
    public List<NetworkControlImmunityStateData> immunities = new();
    public byte hardControlImmunityEnabled;
    public float hardControlImmunityDurationMultiplier;
    public float hardControlImmunityMinimumSeconds;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.TryGetEntity(unitId, out Entity entity))
            return;

        UnitControlRuntimeComponent runtime = CreateRuntime(context);
        // 无预测历史的初始/迟到快照也不能从接收时刻重新开始整个控制时间。
        if (context.IsClient && context.EntityManager.HasComponent<NetworkPlayerComponent>(entity) &&
            FrameManagerUtility.TryGet(context.EntityManager, out ClientFrameManager frame) && frame.running &&
            frame.currentFrame > context.Frame + 1UL)
            UnitControlUtility.TickAndRefresh(ref runtime,
                (frame.currentFrame - context.Frame - 1) * context.FrameInterval / 1000f);
        runtime.NetworkDirty = 0;
        context.SetOrAdd(entity, runtime);
        ApplyPresentation(context, entity);
    }

    public void ApplyAtFrame(NetworkStateApplyContext context)
    {
        if (!context.TryGetEntity(unitId, out Entity entity))
            return;
        context.SetOrAdd(entity, CreateRuntime(context));
        ApplyPresentation(context, entity);
    }

    public UnitControlRuntimeComponent CreateRuntime(NetworkStateApplyContext context)
    {
        UnitControlRuntimeComponent runtime = new()
        {
            Entries = new FixedList512Bytes<UnitControlRuntimeEntry>(),
            ActiveType = UnitControlType.None,
            ActiveSourceEntity = Entity.Null,
            HardControlImmunityEnabled = hardControlImmunityEnabled,
            HardControlImmunityDurationMultiplier = hardControlImmunityDurationMultiplier,
            HardControlImmunityMinimumSeconds = hardControlImmunityMinimumSeconds,
        };
        for (int index = 0; index < entries.Count; index++)
        {
            NetworkControlEntryStateData state = entries[index];
            float remainingTime = state.remainingTime;
            if (remainingTime <= 0f)
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
        }

        for (int index = 0; index < immunities.Count; index++)
        {
            NetworkControlImmunityStateData state = immunities[index];
            float remainingTime = state.remainingTime;
            if (remainingTime > 0f)
                runtime.Immunities.Add(new UnitControlImmunityEntry { ControlType = state.controlType, RemainingTime = remainingTime });
        }

        UnitControlUtility.RefreshResolvedState(ref runtime);
        runtime.NetworkDirty = 0;
        return runtime;
    }

    public void ApplyPresentation(NetworkStateApplyContext context, Entity entity)
    {
        if (context.IsClient)
        {
            UnitControlRuntimeComponent runtime = CreateRuntime(context);
            uint activeEndFrame = 0;
            for (int index = 0; index < entries.Count; index++)
                if (entries[index].controlType == runtime.ActiveType)
                    activeEndFrame = entries[index].endFrame;
            ApplyPresentationState(context, entity, runtime, activeEndFrame);
        }
    }

    public static NetworkControlStateData Capture(EntityManager manager, Guid unitId,
        in UnitControlRuntimeComponent runtime, uint frame, int frameInterval)
    {
        NetworkControlStateData state = new()
        {
            unitId = unitId,
            hardControlImmunityEnabled = runtime.HardControlImmunityEnabled,
            hardControlImmunityDurationMultiplier = runtime.HardControlImmunityDurationMultiplier,
            hardControlImmunityMinimumSeconds = runtime.HardControlImmunityMinimumSeconds,
        };
        for (int i = 0; i < runtime.Entries.Length; i++)
        {
            UnitControlRuntimeEntry entry = runtime.Entries[i];
            Guid sourceId = entry.SourceEntity != Entity.Null && manager.Exists(entry.SourceEntity) &&
                            manager.HasComponent<NetworkIdentityComponent>(entry.SourceEntity)
                ? manager.GetComponentData<NetworkIdentityComponent>(entry.SourceEntity).id : Guid.Empty;
            state.entries.Add(new NetworkControlEntryStateData
            {
                controlType = entry.ControlType, remainingTime = entry.RemainingTime,
                endFrame = GetEndFrame(entry.RemainingTime, frame, frameInterval), priority = entry.Priority,
                lockMove = entry.LockMove, lockCast = entry.LockCast, interruptOnApply = entry.InterruptOnApply,
                sourceUnitId = sourceId, motionVelocityX = entry.MotionVelocity.x,
                motionVelocityY = entry.MotionVelocity.y, motionDamping = entry.MotionDamping,
            });
        }
        for (int i = 0; i < runtime.Immunities.Length; i++)
        {
            UnitControlImmunityEntry entry = runtime.Immunities[i];
            state.immunities.Add(new NetworkControlImmunityStateData
            {
                controlType = entry.ControlType, remainingTime = entry.RemainingTime,
                endFrame = GetEndFrame(entry.RemainingTime, frame, frameInterval),
            });
        }
        return state;
    }

    private static uint GetEndFrame(float seconds, uint frame, int interval)
    {
        double count = Math.Ceiling(Math.Max(0f, seconds) * 1000d / Math.Max(1, interval));
        return count >= uint.MaxValue - frame ? uint.MaxValue : frame + (uint)count;
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
    // 精确保存该权威帧末尾的模拟计时；endFrame 用于表现时钟。
    public float remainingTime;
    public int priority;
    public byte lockMove;
    public byte lockCast;
    public byte interruptOnApply;
    public Guid sourceUnitId;
    public float motionVelocityX;
    public float motionVelocityY;
    public float motionDamping;
}

[Serializable]
public sealed class NetworkControlImmunityStateData
{
    public UnitControlType controlType;
    public uint endFrame;
    public float remainingTime;
}
