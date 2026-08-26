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
    private readonly HashSet<string> _missingResources = new(StringComparer.Ordinal);
    private UnitAnimationFrameLibrary _frameLibrary;
    private const string FrameLibraryPath = "Assets/Res/Data/UnitAnimationFrameLibrary.asset";

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
        if (ResourceComponent.Instance != null && _frameLibrary != null)
            ResourceComponent.Instance.Unload(FrameLibraryPath);

        _frameLibrary = null;
        _missingResources.Clear();
        base.OnDestroy();
    }

    private void UpdateAnimation(
        Entity entity,
        DataTable<UnitAnimationProfileData> profileTable,
        float deltaTime,
        UnitAnimationComponent animation)
    {
        SpriteRenderer spriteRenderer = EntityManager.GetComponentObject<SpriteRenderer>(entity);
        animation.Renderer = spriteRenderer;

        string animationName = animation.CurrentAnimationName.ToString();
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

        UnitAnimationDirection direction = ResolveAnimationDirection(entity, EntityManager, entry, animation);
        string clipPath = entry.GetClipPath(direction);
        if (string.IsNullOrWhiteSpace(clipPath))
        {
            LogMissingOnce(
                $"direction:{profile.UnitDataId}:{animationName}:{direction}",
                $"[UnitAnimationSystem] UnitData {profile.UnitDataId} animation '{animationName}' has no {direction} AnimationClip.");
            return;
        }

        UnitAnimationFrameTrack track = GetFrameTrack(clipPath);
        if (track == null)
            return;

        animation.CurrentAnimationClip = track.SourceClip;

        if (!animation.PlayingAnimationName.Equals(animation.CurrentAnimationName))
        {
            animation.PlayingAnimationName = animation.CurrentAnimationName;
            animation.ElapsedSeconds = 0f;
        }
        else
        {
            animation.ElapsedSeconds += deltaTime;
        }

        float sampleTime = GetSampleTime(track.Length, track.IsLooping, animation.ElapsedSeconds);
        animation.CurrentSampleTime = sampleTime;
        spriteRenderer.sprite = track.SampleSprite(sampleTime);
        if (track.TrySampleFlipX(sampleTime, out bool flipX))
            spriteRenderer.flipX = flipX;
        animation.CurrentSprite = spriteRenderer.sprite;
    }

    private UnitAnimationFrameTrack GetFrameTrack(string clipPath)
    {
        if (_frameLibrary == null)
            _frameLibrary = ResourceComponent.Instance.Load<UnitAnimationFrameLibrary>(FrameLibraryPath);

        if (_frameLibrary == null)
        {
            LogMissingOnce(FrameLibraryPath, $"[UnitAnimationSystem] Missing animation frame library: {FrameLibraryPath}");
            return null;
        }

        UnitAnimationFrameTrack track = _frameLibrary.Find(clipPath);
        if (track == null)
        {
            LogMissingOnce(clipPath, $"[UnitAnimationSystem] Missing animation frames for clip: {clipPath}");
            return null;
        }

        return track;
    }

    private static float GetSampleTime(float length, bool isLooping, float elapsedSeconds)
    {
        length = math.max(0f, length);
        if (length <= 0f)
            return 0f;

        float elapsed = math.max(0f, elapsedSeconds);
        return isLooping
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

    private static UnitAnimationDirection ResolveAnimationDirection(
        Entity entity,
        EntityManager entityManager,
        UnitAnimationEntryData entry,
        UnitAnimationComponent animation)
    {
        return entry.DirectionMode == UnitAnimationDirectionMode.TwoDirections
            ? ResolveTwoDirectionAnimationDirection(entity, entityManager, animation)
            : ResolveFourDirectionAnimationDirection(entity, entityManager);
    }

    private static UnitAnimationDirection ResolveTwoDirectionAnimationDirection(
        Entity entity,
        EntityManager entityManager,
        UnitAnimationComponent animation)
    {
        if (!UnitFacingUtility.TryGetFacing(entityManager, entity, out float2 facingDirection))
            return animation.LastTwoDirectionFacing;

        if (facingDirection.x < -0.0001f)
            animation.LastTwoDirectionFacing = UnitAnimationDirection.Left;
        else if (facingDirection.x > 0.0001f)
            animation.LastTwoDirectionFacing = UnitAnimationDirection.Right;

        return animation.LastTwoDirectionFacing;
    }

    private static UnitAnimationDirection ResolveFourDirectionAnimationDirection(Entity entity, EntityManager entityManager)
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
        animation.PlayingAnimationName = default;
        animation.ElapsedSeconds = 0f;
        animation.CurrentAnimationClip = null;
        animation.CurrentSampleTime = 0f;
        animation.CurrentSprite = null;
    }
}
