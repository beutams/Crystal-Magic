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
    private readonly Dictionary<string, UnitSpriteAnimationClip> _clipCache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _missingClipPaths = new(StringComparer.Ordinal);

    protected override void OnUpdate()
    {
        if (ResourceComponent.Instance == null || DataComponent.Instance == null)
            return;

        DataTable<UnitAnimationProfileData> profileTable = DataComponent.Instance.GetTable<UnitAnimationProfileData>();
        if (profileTable == null)
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;
        List<PendingAnimatedSpriteApply> pendingSpriteApplies = null;
        foreach ((RefRW<UnitAnimationComponent> animation, UnitStateMachineComponent stateMachine, Entity entity) in
                 SystemAPI.Query<RefRW<UnitAnimationComponent>, UnitStateMachineComponent>().WithEntityAccess())
        {
            UpdateAnimation(entity, stateMachine, profileTable, deltaTime, ref animation.ValueRW, ref pendingSpriteApplies);
        }

        if (pendingSpriteApplies == null)
            return;

        for (int i = 0; i < pendingSpriteApplies.Count; i++)
            ApplyQueuedSprite(pendingSpriteApplies[i]);
    }

    protected override void OnDestroy()
    {
        if (ResourceComponent.Instance != null)
        {
            foreach (string path in _clipCache.Keys)
                ResourceComponent.Instance.Unload(path);
        }

        _clipCache.Clear();
        _missingClipPaths.Clear();
        base.OnDestroy();
    }

    private void UpdateAnimation(
        Entity entity,
        UnitStateMachineComponent stateMachine,
        DataTable<UnitAnimationProfileData> profileTable,
        float deltaTime,
        ref UnitAnimationComponent animation,
        ref List<PendingAnimatedSpriteApply> pendingSpriteApplies)
    {
        UnitAnimationProfileData profile = FindProfile(profileTable, stateMachine);
        if (profile == null)
        {
            ResetAnimation(entity, ref animation, 0, -1);
            return;
        }

        string stateName = stateMachine.CurrentStateName ?? "None";
        int stateHash = StringComparer.Ordinal.GetHashCode(stateName);
        string activeSkillName = ResolveActiveSkillName(entity, stateName);
        int activeSkillHash = GetStableHash(activeSkillName);
        UnitAnimationEntryData entry = ResolveAnimationEntry(profile, stateName, activeSkillName);
        if (entry == null || string.IsNullOrWhiteSpace(entry.SpriteClipPath))
        {
            ResetAnimation(entity, ref animation, stateHash, activeSkillHash);
            return;
        }

        UnitSpriteAnimationClip clip = GetClip(entry.SpriteClipPath);
        if (clip == null)
        {
            ResetAnimation(entity, ref animation, stateHash, activeSkillHash);
            return;
        }

        int entryHash = GetEntryHash(stateName, entry);
        bool clipChanged = animation.ClipId != entryHash;
        bool stateChanged = animation.LastStateHash != stateHash || animation.LastSkillId != activeSkillHash;
        if (clipChanged || stateChanged)
        {
            animation.ClipId = entryHash;
            animation.FrameIndex = -1;
            animation.ElapsedSeconds = 0f;
            animation.IsCurrentClipFinished = 0;
            animation.IsCurrentClipLooping = clip.Loop ? (byte)1 : (byte)0;
        }
        else
        {
            animation.ElapsedSeconds += deltaTime * math.max(0.01f, profile.PlaybackSpeed * animation.SpeedMultiplier);
        }

        UnitAnimationDirection direction = ResolveAnimationDirection(entity, EntityManager);
        if (!clip.TryGetFrame(direction, animation.ElapsedSeconds, out Sprite sprite, out int frameIndex, out bool mirrorX))
        {
            ResetAnimation(entity, ref animation, stateHash, activeSkillHash);
            return;
        }

        animation.IsCurrentClipFinished = clip.IsFinished(direction, animation.ElapsedSeconds) ? (byte)1 : (byte)0;

        int textureInstanceId = sprite.texture.GetInstanceID();
        if (clipChanged || animation.LastTextureInstanceId != textureInstanceId)
        {
            if (!UnitAnimationVisualUtility.TryResolveAnimatedSprite(animation.VisualKey, sprite, out Mesh mesh, out Material material))
            {
                ResetAnimation(entity, ref animation, stateHash, activeSkillHash);
                return;
            }

            pendingSpriteApplies ??= new List<PendingAnimatedSpriteApply>();
            pendingSpriteApplies.Add(new PendingAnimatedSpriteApply(entity, mesh, material));
            animation.LastTextureInstanceId = textureInstanceId;
        }

        int directionalVariantHash = GetDirectionalVariantHash(direction, mirrorX);
        if (frameIndex != animation.FrameIndex || animation.LastDirectionalVariantHash != directionalVariantHash)
        {
            animation.FrameIndex = frameIndex;
            ApplyFrameProperties(entity, clip, sprite, mirrorX);
        }

        animation.LastStateHash = stateHash;
        animation.LastSkillId = activeSkillHash;
        animation.LastDirectionalVariantHash = directionalVariantHash;
    }

    private UnitSpriteAnimationClip GetClip(string path)
    {
        if (_clipCache.TryGetValue(path, out UnitSpriteAnimationClip cachedClip))
            return cachedClip;
        if (_missingClipPaths.Contains(path))
            return null;

        UnitSpriteAnimationClip clip = ResourceComponent.Instance.Load<UnitSpriteAnimationClip>(path);
        if (clip == null)
        {
            _missingClipPaths.Add(path);
            Debug.LogWarning($"[UnitAnimationSystem] Missing sprite animation clip: {path}");
            return null;
        }

        _clipCache[path] = clip;
        return clip;
    }

    private void ApplyQueuedSprite(PendingAnimatedSpriteApply pending)
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

    private static UnitAnimationEntryData ResolveAnimationEntry(
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

    private static UnitAnimationDirection ResolveAnimationDirection(Entity entity, EntityManager entityManager)
    {
        if (!UnitFacingUtility.TryGetFacing(entityManager, entity, out float2 facingDirection))
            return UnitAnimationDirection.Front;

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
        if (stateName.IndexOf("CastState", StringComparison.Ordinal) < 0 ||
            !EntityManager.HasComponent<UnitCastComponent>(entity))
        {
            return string.Empty;
        }

        UnitCastComponent cast = EntityManager.GetComponentData<UnitCastComponent>(entity);
        if (!cast.IsCasting || cast.CurrentSkillId < 0)
            return string.Empty;

        SkillData skillData = DataComponent.Instance.Get<SkillData>(cast.CurrentSkillId);
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

    private static int GetEntryHash(string stateName, UnitAnimationEntryData entry)
    {
        return GetStableHash($"{stateName}|{entry.AnimationName}|{entry.SpriteClipPath}");
    }

    private static int GetStableHash(string value)
    {
        return string.IsNullOrEmpty(value) ? 0 : StringComparer.Ordinal.GetHashCode(value);
    }

    private static int GetDirectionalVariantHash(UnitAnimationDirection direction, bool mirrorX)
    {
        return ((int)direction * 2) + (mirrorX ? 1 : 0) + 1;
    }

    private void ApplyFrameProperties(Entity entity, UnitSpriteAnimationClip clip, Sprite sprite, bool mirrorX)
    {
        if (entity == Entity.Null || !EntityManager.Exists(entity) || sprite.texture == null)
            return;

        Rect textureRect = sprite.textureRect;
        Vector2 referencePixels = clip.ReferenceFrameSizePixels;
        Vector2 referenceWorldSize = clip.ReferenceFrameWorldSize;
        Vector2 spriteSize = sprite.rect.size;
        Vector2 worldSize = new(
            spriteSize.x / referencePixels.x * referenceWorldSize.x,
            spriteSize.y / referencePixels.y * referenceWorldSize.y);
        Vector2 pivotOffset = new(
            worldSize.x * 0.5f - sprite.pivot.x / referencePixels.x * referenceWorldSize.x,
            worldSize.y * 0.5f - sprite.pivot.y / referencePixels.y * referenceWorldSize.y);

        float uvWidth = textureRect.width / sprite.texture.width;
        float uvHeight = textureRect.height / sprite.texture.height;
        float uvMinX = textureRect.x / sprite.texture.width;
        float uvMinY = textureRect.y / sprite.texture.height;
        if (mirrorX)
        {
            uvMinX += uvWidth;
            uvWidth = -uvWidth;
            pivotOffset.x = -pivotOffset.x;
        }

        EntityManager.SetComponentData(entity, new UnitAnimationFrameUvMinProperty
        {
            Value = new float4(uvMinX, uvMinY, 0f, 0f),
        });
        EntityManager.SetComponentData(entity, new UnitAnimationFrameUvSizeProperty
        {
            Value = new float4(uvWidth, uvHeight, 0f, 0f),
        });
        EntityManager.SetComponentData(entity, new UnitAnimationFrameWorldSizeProperty
        {
            Value = new float4(worldSize.x, worldSize.y, 0f, 0f),
        });
        EntityManager.SetComponentData(entity, new UnitAnimationFramePivotOffsetProperty
        {
            Value = new float4(pivotOffset.x, pivotOffset.y, 0f, 0f),
        });
    }

    private void ResetAnimation(Entity entity, ref UnitAnimationComponent animation, int stateHash, int skillHash)
    {
        ResetFrameProperties(entity);
        animation.ClipId = -1;
        animation.FrameIndex = -1;
        animation.LastTextureInstanceId = 0;
        animation.LastStateHash = stateHash;
        animation.LastSkillId = skillHash;
        animation.LastDirectionalVariantHash = 0;
        animation.IsCurrentClipFinished = 0;
        animation.IsCurrentClipLooping = 0;
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

    private readonly struct PendingAnimatedSpriteApply
    {
        public PendingAnimatedSpriteApply(Entity entity, Mesh mesh, Material material)
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
