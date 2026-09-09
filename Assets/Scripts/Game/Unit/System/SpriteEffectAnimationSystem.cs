using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(SkillProjectileSystem))]
[UpdateBefore(typeof(DestroyEntitySystem))]
public partial class SpriteEffectAnimationSystem : SystemBase
{
    private const string FrameLibraryPath = "Assets/Res/Data/UnitAnimationFrameLibrary.asset";

    private UnitAnimationFrameLibrary _frameLibrary;

    protected override void OnUpdate()
    {
        if (ResourceComponent.Instance == null)
            return;

        _frameLibrary ??= ResourceComponent.Instance.Load<UnitAnimationFrameLibrary>(FrameLibraryPath);
        if (_frameLibrary == null)
            return;

        float deltaTime = SystemAPI.Time.DeltaTime;
        List<Entity> pendingDestroy = null;
        foreach ((SpriteEffectAnimationComponent animation, Entity entity) in
                 SystemAPI.Query<SpriteEffectAnimationComponent>().WithEntityAccess())
        {
            UpdateFollow(entity, animation);
            UpdateAnimation(entity, animation, deltaTime, ref pendingDestroy);
        }

        ApplyPendingDestroy(pendingDestroy);
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

    private void UpdateFollow(Entity entity, SpriteEffectAnimationComponent animation)
    {
        if (!EntityManager.HasComponent<EffectVisualFollowComponent>(entity) ||
            !EntityManager.HasComponent<LocalTransform>(entity))
        {
            return;
        }

        EffectVisualFollowComponent follow = EntityManager.GetComponentData<EffectVisualFollowComponent>(entity);
        if (follow.Target == Entity.Null || !EntityManager.Exists(follow.Target) ||
            !EntityManager.HasComponent<LocalTransform>(follow.Target))
        {
            if (follow.EndWhenTargetMissing != 0)
                animation.EndRequested = 1;
            return;
        }

        LocalTransform targetTransform = EntityManager.GetComponentData<LocalTransform>(follow.Target);
        LocalTransform visualTransform = EntityManager.GetComponentData<LocalTransform>(entity);
        quaternion rotation = follow.AlignRotation != 0 ? targetTransform.Rotation : visualTransform.Rotation;
        visualTransform.Position = targetTransform.Position + math.rotate(rotation, follow.Offset);
        if (follow.AlignRotation != 0)
            visualTransform.Rotation = rotation;
        EntityManager.SetComponentData(entity, visualTransform);
    }

    private void UpdateAnimation(
        Entity entity,
        SpriteEffectAnimationComponent animation,
        float deltaTime,
        ref List<Entity> pendingDestroy)
    {
        animation.Renderer = EntityManager.GetComponentObject<SpriteRenderer>(entity);
        if (!EnsureActivePhase(animation, ref pendingDestroy, entity))
            return;

        if (animation.Phase == SpriteEffectAnimationPhase.Loop && animation.RemainingLoopSeconds >= 0f)
        {
            animation.RemainingLoopSeconds = math.max(0f, animation.RemainingLoopSeconds - deltaTime);
            if (animation.RemainingLoopSeconds <= 0f)
                animation.EndRequested = 1;
        }

        if (animation.Phase == SpriteEffectAnimationPhase.Loop && animation.EndRequested != 0)
        {
            if (!BeginExitOrDestroy(animation, ref pendingDestroy, entity))
                return;
        }

        UnitAnimationFrameTrack track = _frameLibrary.Find(animation.CurrentClip);
        if (track == null)
        {
            QueueDestroy(entity, ref pendingDestroy);
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
            AdvanceOneShot(animation, ref pendingDestroy, entity);
    }

    private static bool EnsureActivePhase(
        SpriteEffectAnimationComponent animation,
        ref List<Entity> pendingDestroy,
        Entity entity)
    {
        if (animation.Phase != SpriteEffectAnimationPhase.Uninitialized)
            return true;

        if (animation.EnterClip != null)
        {
            BeginPhase(animation, SpriteEffectAnimationPhase.Enter, animation.EnterClip);
            return true;
        }

        if (animation.LoopClip != null && animation.EndRequested == 0)
        {
            BeginPhase(animation, SpriteEffectAnimationPhase.Loop, animation.LoopClip);
            return true;
        }

        return BeginExitOrDestroy(animation, ref pendingDestroy, entity);
    }

    private static void AdvanceOneShot(
        SpriteEffectAnimationComponent animation,
        ref List<Entity> pendingDestroy,
        Entity entity)
    {
        if (animation.Phase == SpriteEffectAnimationPhase.Enter &&
            animation.LoopClip != null && animation.EndRequested == 0)
        {
            BeginPhase(animation, SpriteEffectAnimationPhase.Loop, animation.LoopClip);
            return;
        }

        BeginExitOrDestroy(animation, ref pendingDestroy, entity);
    }

    private static bool BeginExitOrDestroy(
        SpriteEffectAnimationComponent animation,
        ref List<Entity> pendingDestroy,
        Entity entity)
    {
        if (animation.Phase != SpriteEffectAnimationPhase.Exit && animation.ExitClip != null)
        {
            BeginPhase(animation, SpriteEffectAnimationPhase.Exit, animation.ExitClip);
            return true;
        }

        QueueDestroy(entity, ref pendingDestroy);
        return false;
    }

    private static void BeginPhase(
        SpriteEffectAnimationComponent animation,
        SpriteEffectAnimationPhase phase,
        AnimationClip clip)
    {
        animation.Phase = phase;
        animation.CurrentClip = clip;
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

    private static void QueueDestroy(Entity entity, ref List<Entity> pendingDestroy)
    {
        pendingDestroy ??= new List<Entity>();
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
