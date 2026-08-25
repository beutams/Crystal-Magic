using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(StateScriptSystem))]
partial class UnitAnimationSystem : SystemBase
{
    private readonly Dictionary<string, AnimationClip> _clipCache = new(StringComparer.Ordinal);
    private readonly HashSet<string> _missingResources = new(StringComparer.Ordinal);
    private readonly HashSet<Entity> _missingRenderers = new();

    protected override void OnUpdate()
    {
        if (ResourceComponent.Instance == null || DataComponent.Instance == null)
            return;

        DataTable<UnitAnimationProfileData> profileTable = DataComponent.Instance.GetTable<UnitAnimationProfileData>();
        if (profileTable == null)
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;
        foreach ((UnitAnimationComponent animation, Entity entity) in
                 SystemAPI.Query<UnitAnimationComponent>().WithEntityAccess())
        {
            UpdateAnimation(entity, profileTable, deltaTime, animation);
        }
    }

    protected override void OnDestroy()
    {
        if (ResourceComponent.Instance != null)
        {
            foreach (string path in _clipCache.Keys)
                ResourceComponent.Instance.Unload(path);
        }

        _clipCache.Clear();
        _missingResources.Clear();
        _missingRenderers.Clear();
        base.OnDestroy();
    }

    private void UpdateAnimation(
        Entity entity,
        DataTable<UnitAnimationProfileData> profileTable,
        float deltaTime,
        UnitAnimationComponent animation)
    {
        SpriteRenderer spriteRenderer = animation.Renderer;
        if (spriteRenderer == null)
        {
            if (_missingRenderers.Add(entity))
                Debug.LogWarning($"[UnitAnimationSystem] {entity} has UnitAnimationAuthoring but no SpriteRenderer on the same GameObject.");
            return;
        }

        string animationName = animation.Name.ToString();
        if (string.IsNullOrWhiteSpace(animationName))
        {
            ResetPlayback(animation);
            return;
        }

        UnitAnimationProfileData profile = FindProfile(profileTable, entity);
        if (profile == null)
        {
            LogMissingOnce($"profile:{entity}", $"[UnitAnimationSystem] Missing animation profile for {entity}.");
            return;
        }

        UnitAnimationEntryData entry = FindEntry(profile, animationName);
        if (entry == null)
        {
            LogMissingOnce(
                $"entry:{profile.UnitDataId}:{animationName}",
                $"[UnitAnimationSystem] UnitData {profile.UnitDataId} has no animation named '{animationName}'.");
            return;
        }

        UnitAnimationDirection direction = ResolveAnimationDirection(entity, EntityManager);
        string clipPath = entry.GetClipPath(direction);
        if (string.IsNullOrWhiteSpace(clipPath))
        {
            LogMissingOnce(
                $"direction:{profile.UnitDataId}:{animationName}:{direction}",
                $"[UnitAnimationSystem] UnitData {profile.UnitDataId} animation '{animationName}' has no {direction} AnimationClip.");
            return;
        }

        AnimationClip clip = GetClip(clipPath);
        if (clip == null)
            return;

        if (!animation.PlayingName.Equals(animation.Name))
        {
            animation.PlayingName = animation.Name;
            animation.ElapsedSeconds = 0f;
        }
        else
        {
            animation.ElapsedSeconds += deltaTime;
        }

        float sampleTime = GetSampleTime(clip, animation.ElapsedSeconds);
        clip.SampleAnimation(spriteRenderer.gameObject, sampleTime);
    }

    private AnimationClip GetClip(string path)
    {
        if (_clipCache.TryGetValue(path, out AnimationClip cachedClip))
            return cachedClip;
        if (_missingResources.Contains(path))
            return null;

        AnimationClip clip = ResourceComponent.Instance.Load<AnimationClip>(path);
        if (clip == null)
        {
            LogMissingOnce(path, $"[UnitAnimationSystem] Missing AnimationClip: {path}");
            return null;
        }

        _clipCache[path] = clip;
        return clip;
    }

    private static float GetSampleTime(AnimationClip clip, float elapsedSeconds)
    {
        float length = math.max(0f, clip.length);
        if (length <= 0f)
            return 0f;

        float elapsed = math.max(0f, elapsedSeconds);
        return clip.isLooping
            ? elapsed % length
            : math.min(elapsed, length);
    }

    private static UnitAnimationEntryData FindEntry(UnitAnimationProfileData profile, string animationName)
    {
        profile.Normalize();
        for (int i = 0; i < profile.Animations.Count; i++)
        {
            UnitAnimationEntryData entry = profile.Animations[i];
            if (entry != null && string.Equals(entry.Name, animationName, StringComparison.Ordinal))
                return entry;
        }

        return null;
    }

    private UnitAnimationProfileData FindProfile(DataTable<UnitAnimationProfileData> profileTable, Entity entity)
    {
        if (!EntityManager.HasComponent<UnitStateScriptComponent>(entity))
            return null;

        UnitStateScriptComponent stateScript = EntityManager.GetComponentObject<UnitStateScriptComponent>(entity);
        if (stateScript == null || stateScript.UnitDataId < 0)
            return null;

        foreach (UnitAnimationProfileData profile in profileTable.GetAll())
        {
            if (profile != null && profile.UnitDataId == stateScript.UnitDataId)
                return profile;
        }

        return null;
    }

    private static UnitAnimationDirection ResolveAnimationDirection(Entity entity, EntityManager entityManager)
    {
        if (!UnitFacingUtility.TryGetFacing(entityManager, entity, out float2 facingDirection))
            return UnitAnimationDirection.Front;

        float2 direction = math.normalizesafe(facingDirection, new float2(0f, -1f));
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

    private void LogMissingOnce(string key, string message)
    {
        if (_missingResources.Add(key))
            Debug.LogWarning(message);
    }

    private static void ResetPlayback(UnitAnimationComponent animation)
    {
        animation.PlayingName = default;
        animation.ElapsedSeconds = 0f;
    }
}
