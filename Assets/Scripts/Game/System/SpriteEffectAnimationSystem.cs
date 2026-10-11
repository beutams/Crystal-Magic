using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[WorldSystemFilter(WorldSystemFilterFlags.LocalSimulation | WorldSystemFilterFlags.ClientSimulation)]
[UpdateInGroup(typeof(GamePresentationSystemGroup))]
public partial class SpriteEffectAnimationSystem : SystemBase
{
    private const string FrameLibraryPath = "Assets/Res/Data/UnitAnimationFrameLibrary.asset";

    private readonly List<Entity> _pendingDestroy = new();
    private readonly HashSet<string> _missingTracks = new();
    private UnitAnimationFrameLibrary _frameLibrary;

    protected override void OnUpdate()
    {
        if (ResourceComponent.Instance == null)
            return;

        _frameLibrary ??= ResourceComponent.Instance.Load<UnitAnimationFrameLibrary>(FrameLibraryPath);
        if (_frameLibrary == null)
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;
        _pendingDestroy.Clear();
        foreach ((SpriteEffectAnimationComponent animation, Entity entity) in
                 SystemAPI.Query<SpriteEffectAnimationComponent>().WithEntityAccess())
        {
            UpdateAnimation(entity, animation, deltaTime, _pendingDestroy);
        }

        ApplyPendingDestroy(_pendingDestroy);
    }

    protected override void OnDestroy()
    {
        if (ResourceComponent.Instance != null && _frameLibrary != null)
            ResourceComponent.Instance.Unload(FrameLibraryPath);

        _frameLibrary = null;
        base.OnDestroy();
    }

    public static void RequestEnd(EntityManager entityManager, Entity entity)
    {
        if (entity == Entity.Null || !entityManager.Exists(entity) ||
            !entityManager.HasComponent<SpriteEffectAnimationComponent>(entity))
        {
            return;
        }

        SpriteEffectAnimationComponent animation = entityManager.GetComponentObject<SpriteEffectAnimationComponent>(entity);
        animation.EndRequested = 1;
    }

    private void UpdateAnimation(
        Entity entity,
        SpriteEffectAnimationComponent animation,
        float deltaTime,
        List<Entity> pendingDestroy)
    {
        animation.Renderer = EntityManager.GetComponentObject<SpriteRenderer>(entity);
        if (!EnsureActivePhase(animation, pendingDestroy, entity))
            return;

        if (animation.Phase == SpriteEffectAnimationPhase.Loop && animation.RemainingLoopSeconds >= 0f)
        {
            animation.RemainingLoopSeconds = math.max(0f, animation.RemainingLoopSeconds - deltaTime);
            if (animation.RemainingLoopSeconds <= 0f)
                animation.EndRequested = 1;
        }

        if (animation.Phase == SpriteEffectAnimationPhase.Loop && animation.EndRequested != 0)
        {
            if (!BeginExitOrDestroy(animation, pendingDestroy, entity))
                return;
        }

        UnitAnimationFrameTrack track = string.IsNullOrEmpty(animation.CurrentClipPath)
            ? _frameLibrary.Find(animation.CurrentClip)
            : _frameLibrary.Find(animation.CurrentClipPath);
        if (track == null)
        {
            string clipKey = !string.IsNullOrEmpty(animation.CurrentClipPath)
                ? animation.CurrentClipPath
                : animation.CurrentClip != null ? animation.CurrentClip.name : "<unassigned>";
            if (_missingTracks.Add(clipKey))
                Debug.LogError($"[SpriteEffectAnimation] Missing frame track: {clipKey}");
            QueueDestroy(entity, pendingDestroy);
            return;
        }

        animation.PhaseElapsedSeconds += deltaTime;
        bool loop = animation.Phase == SpriteEffectAnimationPhase.Loop;
        animation.CurrentSampleTime = SampleTime(track.Length, animation.PhaseElapsedSeconds, loop);
        animation.Renderer.sprite = track.SampleSprite(animation.CurrentSampleTime);
        if (track.TrySampleFlipX(animation.CurrentSampleTime, out bool flipX))
            animation.Renderer.flipX = flipX;
        animation.CurrentSprite = animation.Renderer.sprite;

        if (!loop && animation.PhaseElapsedSeconds >= track.Length)
            AdvanceOneShot(animation, pendingDestroy, entity);
    }

    private static bool EnsureActivePhase(
        SpriteEffectAnimationComponent animation,
        List<Entity> pendingDestroy,
        Entity entity)
    {
        if (animation.Phase != SpriteEffectAnimationPhase.Uninitialized)
            return true;

        if (animation.EnterClip != null)
        {
            BeginPhase(animation, SpriteEffectAnimationPhase.Enter, animation.EnterClip, animation.EnterClipPath);
            return true;
        }

        if (animation.LoopClip != null && animation.EndRequested == 0)
        {
            BeginPhase(animation, SpriteEffectAnimationPhase.Loop, animation.LoopClip, animation.LoopClipPath);
            return true;
        }

        return BeginExitOrDestroy(animation, pendingDestroy, entity);
    }

    private static void AdvanceOneShot(
        SpriteEffectAnimationComponent animation,
        List<Entity> pendingDestroy,
        Entity entity)
    {
        if (animation.Phase == SpriteEffectAnimationPhase.Enter &&
            animation.LoopClip != null && animation.EndRequested == 0)
        {
            BeginPhase(animation, SpriteEffectAnimationPhase.Loop, animation.LoopClip, animation.LoopClipPath);
            return;
        }

        BeginExitOrDestroy(animation, pendingDestroy, entity);
    }

    private static bool BeginExitOrDestroy(
        SpriteEffectAnimationComponent animation,
        List<Entity> pendingDestroy,
        Entity entity)
    {
        if (animation.Phase != SpriteEffectAnimationPhase.Exit && animation.ExitClip != null)
        {
            BeginPhase(animation, SpriteEffectAnimationPhase.Exit, animation.ExitClip, animation.ExitClipPath);
            return true;
        }

        QueueDestroy(entity, pendingDestroy);
        return false;
    }

    private static void BeginPhase(
        SpriteEffectAnimationComponent animation,
        SpriteEffectAnimationPhase phase,
        AnimationClip clip,
        string clipPath)
    {
        animation.Phase = phase;
        animation.CurrentClip = clip;
        animation.CurrentClipPath = clipPath;
        animation.CurrentAnimationName = new Unity.Collections.FixedString64Bytes(clip.name);
        animation.PhaseElapsedSeconds = 0f;
        animation.CurrentSampleTime = 0f;
    }

    private static float SampleTime(float length, float elapsed, bool loop)
    {
        if (length <= 0f)
            return 0f;

        float clampedElapsed = math.max(0f, elapsed);
        return loop ? clampedElapsed % length : math.min(clampedElapsed, length);
    }

    private static void QueueDestroy(Entity entity, List<Entity> pendingDestroy)
    {
        pendingDestroy.Add(entity);
    }

    private void ApplyPendingDestroy(List<Entity> pendingDestroy)
    {
        if (pendingDestroy == null)
            return;

        for (int index = 0; index < pendingDestroy.Count; index++)
        {
            Entity entity = pendingDestroy[index];
            if (!EntityManager.Exists(entity))
                continue;

            if (!EntityManager.HasComponent<DestroyEntityFlag>(entity))
                EntityManager.AddComponent<DestroyEntityFlag>(entity);
            EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
        }
    }
}
