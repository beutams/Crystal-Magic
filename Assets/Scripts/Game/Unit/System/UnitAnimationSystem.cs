using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
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
        List<PendingAnimatedAtlasApply> pendingAtlasApplies = null;
        foreach ((RefRW<UnitAnimationComponent> animation, UnitStateMachineComponent stateMachine, Entity entity) in
                 SystemAPI.Query<RefRW<UnitAnimationComponent>, UnitStateMachineComponent>().WithEntityAccess())
        {
            UpdateAnimation(entity, stateMachine, profileTable, deltaTime, ref animation.ValueRW, ref pendingAtlasApplies);
        }

        if (pendingAtlasApplies != null)
        {
            for (int i = 0; i < pendingAtlasApplies.Count; i++)
                ApplyQueuedAtlas(pendingAtlasApplies[i]);
        }
    }

    private void UpdateAnimation(
        Entity entity,
        UnitStateMachineComponent stateMachine,
        DataTable<UnitAnimationProfileData> profileTable,
        float deltaTime,
        ref UnitAnimationComponent animation,
        ref List<PendingAnimatedAtlasApply> pendingAtlasApplies)
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
        UnitAnimationDirection direction = ResolveAnimationDirection(entity, EntityManager);

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

        ResolveDirectionalVisual(entry, direction, out string atlasTexturePath, out bool mirrorX);
        int atlasHash = string.IsNullOrWhiteSpace(atlasTexturePath)
            ? 0
            : StringComparer.Ordinal.GetHashCode(atlasTexturePath);
        int directionalVariantHash = GetDirectionalVariantHash(direction, mirrorX);
        if (clipChanged || animation.LastAtlasPathHash != atlasHash)
        {
            if (UnitAnimationVisualUtility.TryResolveAnimatedAtlas(animation.VisualKey, atlasTexturePath, out Mesh mesh, out Material material))
            {
                pendingAtlasApplies ??= new List<PendingAnimatedAtlasApply>();
                pendingAtlasApplies.Add(new PendingAnimatedAtlasApply(entity, mesh, material));
                animation.LastAtlasPathHash = atlasHash;
            }
        }

        int frameIndex = ResolveFrameIndex(entry, animation.ElapsedSeconds);
        if (frameIndex != animation.FrameIndex || animation.LastDirectionalVariantHash != directionalVariantHash)
        {
            animation.FrameIndex = frameIndex;
            ApplyFrameProperties(entity, entry, frameIndex, mirrorX);
        }

        animation.LastStateHash = stateHash;
        animation.LastSkillId = activeSkillHash;
        animation.LastDirectionalVariantHash = directionalVariantHash;
    }

    private void ApplyQueuedAtlas(PendingAnimatedAtlasApply pending)
    {
        if (pending.Entity == Entity.Null ||
            !EntityManager.Exists(pending.Entity) ||
            !EntityManager.HasComponent<MaterialMeshInfo>(pending.Entity))
        {
            return;
        }

        EntityManager.SetSharedComponentManaged(
            pending.Entity,
            new RenderMeshArray(new[] { pending.Material }, new[] { pending.Mesh }));
        EntityManager.SetComponentData(pending.Entity, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));
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

    private static UnitAnimationDirection ResolveAnimationDirection(Entity entity, EntityManager entityManager)
    {
        if (!UnitFacingUtility.TryGetFacing(entityManager, entity, out float2 facingDirection))
        {
            return UnitAnimationDirection.Front;
        }

        return QuantizeToFourDirections(facingDirection);
    }

    private static UnitAnimationDirection QuantizeToFourDirections(float2 rawDirection)
    {
        float2 direction = math.normalizesafe(rawDirection, new float2(0f, -1f));
        float bestDot = float.NegativeInfinity;
        UnitAnimationDirection bestDirection = UnitAnimationDirection.Front;

        EvaluateCardinal(direction, new float2(0f, -1f), UnitAnimationDirection.Front, ref bestDot, ref bestDirection);
        EvaluateCardinal(direction, new float2(0f, 1f), UnitAnimationDirection.Back, ref bestDot, ref bestDirection);
        EvaluateCardinal(direction, new float2(-1f, 0f), UnitAnimationDirection.Left, ref bestDot, ref bestDirection);
        EvaluateCardinal(direction, new float2(1f, 0f), UnitAnimationDirection.Right, ref bestDot, ref bestDirection);

        return bestDirection;
    }

    private static void EvaluateCardinal(
        float2 direction,
        float2 cardinal,
        UnitAnimationDirection candidate,
        ref float bestDot,
        ref UnitAnimationDirection bestDirection)
    {
        float dot = math.dot(direction, cardinal);
        if (dot <= bestDot)
            return;

        bestDot = dot;
        bestDirection = candidate;
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

    private static int GetDirectionalVariantHash(UnitAnimationDirection direction, bool mirrorX)
    {
        return ((int)direction * 2) + (mirrorX ? 1 : 0) + 1;
    }

    private static void ResolveDirectionalVisual(UnitAnimationEntryData entry, UnitAnimationDirection direction, out string atlasTexturePath, out bool mirrorX)
    {
        mirrorX = false;
        atlasTexturePath = direction switch
        {
            UnitAnimationDirection.Back => entry.BackAtlasTexturePath,
            UnitAnimationDirection.Left => entry.LeftAtlasTexturePath,
            UnitAnimationDirection.Right => entry.LeftAtlasTexturePath,
            _ => entry.FrontAtlasTexturePath,
        };

        mirrorX = direction == UnitAnimationDirection.Right && !string.IsNullOrWhiteSpace(entry.LeftAtlasTexturePath);

        if (!string.IsNullOrWhiteSpace(atlasTexturePath))
            return;

        atlasTexturePath = !string.IsNullOrWhiteSpace(entry.FrontAtlasTexturePath)
            ? entry.FrontAtlasTexturePath
            : !string.IsNullOrWhiteSpace(entry.LeftAtlasTexturePath)
                ? entry.LeftAtlasTexturePath
                : entry.BackAtlasTexturePath;
        mirrorX = false;
    }

    private void ApplyFrameProperties(Entity entity, UnitAnimationEntryData entry, int frameIndex, bool mirrorX)
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
        float uvMinX = col * uvWidth;
        float uvMinY = row * uvHeight;
        float frameUvMinX = mirrorX ? uvMinX + uvWidth : uvMinX;
        float frameUvSizeX = mirrorX ? -uvWidth : uvWidth;

        EntityManager.SetComponentData(entity, new UnitAnimationFrameUvMinProperty
        {
            Value = new float4(frameUvMinX, uvMinY, 0f, 0f),
        });
        EntityManager.SetComponentData(entity, new UnitAnimationFrameUvSizeProperty
        {
            Value = new float4(frameUvSizeX, uvHeight, 0f, 0f),
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

    private readonly struct PendingAnimatedAtlasApply
    {
        public PendingAnimatedAtlasApply(Entity entity, Mesh mesh, Material material)
        {
            Entity = entity;
            Mesh = mesh;
            Material = material;
        }

        public Entity Entity { get; }
        public Mesh Mesh { get; }
        public Material Material { get; }
    }
}
