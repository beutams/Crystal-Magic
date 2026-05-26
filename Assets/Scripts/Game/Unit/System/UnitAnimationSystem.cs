using System;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateAfter(typeof(UnitStateTransitionSystem))]
[UpdateAfter(typeof(UnitSkillExecuteSystem))]
partial class UnitAnimationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (ResourceComponent.Instance == null || DataComponent.Instance == null)
            return;

        DataTable<UnitAnimationProfileData> profileTable = DataComponent.Instance.GetTable<UnitAnimationProfileData>();
        if (profileTable == null)
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;
        foreach ((RefRW<UnitAnimationComponent> animation, RefRO<UnitQuadVisualRequest> request, UnitStateMachineComponent stateMachine, Entity entity) in
                 SystemAPI.Query<RefRW<UnitAnimationComponent>, RefRO<UnitQuadVisualRequest>, UnitStateMachineComponent>().WithEntityAccess())
        {
            UpdateAnimation(entity, stateMachine, request.ValueRO, profileTable, deltaTime, ref animation.ValueRW);
        }
    }

    private void UpdateAnimation(
        Entity entity,
        UnitStateMachineComponent stateMachine,
        in UnitQuadVisualRequest visualRequest,
        DataTable<UnitAnimationProfileData> profileTable,
        float deltaTime,
        ref UnitAnimationComponent animation)
    {
        UnitAnimationProfileData profile = FindProfile(profileTable, stateMachine);
        if (profile == null)
        {
            ResetFrameProperties(entity);
            animation.ClipId = -1;
            animation.FrameIndex = -1;
            animation.LastStateHash = 0;
            animation.LastSkillId = -1;
            return;
        }

        string stateName = stateMachine.CurrentStateName ?? "None";
        int stateHash = StringComparer.Ordinal.GetHashCode(stateName);
        string activeSkillName = ResolveActiveSkillName(entity, stateName);

        UnitAnimationEntryData entry = ResolveAnimationEntry(profile, stateName, activeSkillName);
        if (entry == null)
        {
            ResetFrameProperties(entity);
            animation.ClipId = -1;
            animation.FrameIndex = -1;
            animation.LastStateHash = stateHash;
            animation.LastSkillId = GetStableHash(activeSkillName);
            return;
        }

        entry.Normalize();
        int entryHash = GetEntryHash(stateName, entry.AnimationName);
        int activeSkillHash = GetStableHash(activeSkillName);
        bool clipChanged = animation.ClipId != entryHash;
        bool stateChanged = animation.LastStateHash != stateHash || animation.LastSkillId != activeSkillHash;
        if (clipChanged || stateChanged)
        {
            animation.ClipId = entryHash;
            animation.FrameIndex = -1;
            animation.ElapsedSeconds = 0f;
        }
        else
        {
            animation.ElapsedSeconds += deltaTime * math.max(0.01f, profile.PlaybackSpeed * animation.SpeedMultiplier);
        }

        int atlasHash = string.IsNullOrWhiteSpace(entry.AtlasTexturePath)
            ? 0
            : StringComparer.Ordinal.GetHashCode(entry.AtlasTexturePath);
        if (clipChanged || animation.LastAtlasPathHash != atlasHash)
        {
            UnitAnimationVisualUtility.ApplyAnimatedAtlas(EntityManager, entity, visualRequest.VisualKey, entry.AtlasTexturePath);
            animation.LastAtlasPathHash = atlasHash;
        }

        int frameIndex = ResolveFrameIndex(entry, animation.ElapsedSeconds);
        if (frameIndex != animation.FrameIndex)
        {
            animation.FrameIndex = frameIndex;
            ApplyFrameProperties(entity, entry, frameIndex);
        }

        animation.LastStateHash = stateHash;
        animation.LastSkillId = activeSkillHash;
    }

    private UnitAnimationEntryData ResolveAnimationEntry(
        UnitAnimationProfileData profile,
        string stateName,
        string activeSkillName)
    {
        if (profile?.Animations == null)
            return null;

        if (!string.IsNullOrWhiteSpace(activeSkillName))
        {
            for (int i = 0; i < profile.Animations.Count; i++)
            {
                UnitAnimationEntryData entry = profile.Animations[i];
                if (entry == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(entry.StateName) &&
                    !string.Equals(entry.StateName, stateName, StringComparison.Ordinal))
                {
                    continue;
                }
                if (string.Equals(entry.AnimationName, activeSkillName, StringComparison.Ordinal))
                    return entry;
            }
        }

        for (int i = 0; i < profile.Animations.Count; i++)
        {
            UnitAnimationEntryData entry = profile.Animations[i];
            if (entry == null ||
                !string.Equals(entry.StateName, stateName, StringComparison.Ordinal) ||
                !string.IsNullOrWhiteSpace(entry.AnimationName))
            {
                continue;
            }

            return entry;
        }

        return null;
    }

    private static int ResolveFrameIndex(UnitAnimationEntryData entry, float elapsedSeconds)
    {
        int frameCount = entry.FrameCount;
        if (frameCount <= 0)
            return -1;

        float fps = math.max(0.01f, entry.FramesPerSecond);
        int rawIndex = (int)math.floor(math.max(0f, elapsedSeconds) * fps);
        if (entry.Loop)
            return rawIndex % frameCount;

        return math.clamp(rawIndex, 0, frameCount - 1);
    }

    private string ResolveActiveSkillName(Entity entity, string stateName)
    {
        if (stateName.IndexOf("CastState", StringComparison.Ordinal) < 0)
            return string.Empty;

        if (!EntityManager.HasComponent<UnitCastComponent>(entity))
            return string.Empty;

        UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(entity);
        if (!cast.IsCasting || cast.CurrentSkillId < 0)
            return string.Empty;

        SkillData skillData = DataComponent.Instance?.Get<SkillData>(cast.CurrentSkillId);
        return skillData?.DisplayName ?? string.Empty;
    }

    private static UnitAnimationProfileData FindProfile(DataTable<UnitAnimationProfileData> profileTable, UnitStateMachineComponent stateMachine)
    {
        UnitAnimationProfileData fallback = null;
        foreach (UnitAnimationProfileData row in profileTable.GetAll())
        {
            if (row == null)
                continue;

            row.Normalize();
            if (row.UnitDataId >= 0 && row.UnitDataId == stateMachine.UnitDataId)
                return row;

            if (fallback == null &&
                !string.IsNullOrWhiteSpace(row.UnitName) &&
                string.Equals(row.UnitName, stateMachine.UnitName, StringComparison.Ordinal))
            {
                fallback = row;
            }
        }

        return fallback;
    }

    private static int GetEntryHash(string stateName, string animationName)
    {
        return GetStableHash($"{stateName}|{animationName}");
    }

    private static int GetStableHash(string value)
    {
        return string.IsNullOrEmpty(value) ? 0 : StringComparer.Ordinal.GetHashCode(value);
    }

    private void ApplyFrameProperties(Entity entity, UnitAnimationEntryData entry, int frameIndex)
    {
        if (entry == null || entity == Entity.Null || !EntityManager.Exists(entity))
            return;

        entry.Normalize();

        int cols = math.max(1, entry.GridColumns);
        int rows = math.max(1, entry.GridRows);
        float uvWidth = 1f / cols;
        float uvHeight = 1f / rows;
        int col = frameIndex % cols;
        int rowTop = frameIndex / cols;
        int row = (rows - 1) - rowTop;

        EntityManager.SetComponentData(entity, new UnitAnimationFrameUvMinProperty
        {
            Value = new float4(col * uvWidth, row * uvHeight, 0f, 0f),
        });
        EntityManager.SetComponentData(entity, new UnitAnimationFrameUvSizeProperty
        {
            Value = new float4(uvWidth, uvHeight, 0f, 0f),
        });
        EntityManager.SetComponentData(entity, new UnitAnimationFrameWorldSizeProperty
        {
            Value = new float4(1f, 1f, 0f, 0f),
        });
        EntityManager.SetComponentData(entity, new UnitAnimationFramePivotOffsetProperty
        {
            Value = new float4(0f, 0f, 0f, 0f),
        });
    }

    private void ResetFrameProperties(Entity entity)
    {
        if (entity == Entity.Null || !EntityManager.Exists(entity))
            return;

        EntityManager.SetComponentData(entity, new UnitAnimationFrameUvMinProperty
        {
            Value = new float4(0f, 0f, 0f, 0f),
        });
        EntityManager.SetComponentData(entity, new UnitAnimationFrameUvSizeProperty
        {
            Value = new float4(1f, 1f, 0f, 0f),
        });
        EntityManager.SetComponentData(entity, new UnitAnimationFrameWorldSizeProperty
        {
            Value = new float4(1f, 1f, 0f, 0f),
        });
        EntityManager.SetComponentData(entity, new UnitAnimationFramePivotOffsetProperty
        {
            Value = new float4(0f, 0f, 0f, 0f),
        });
    }
}
