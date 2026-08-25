using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public static class QuadOverlayPulseUtility
{
    public static readonly float4 DefaultHitOverlayColor = new(1f, 0.3f, 0.3f, 1f);
    public const float DefaultHitOverlayDuration = 0.12f;
    public const float DefaultHitOverlayStrength = 0.65f;

    public static void Play(
        EntityManager entityManager,
        Entity entity,
        float4 overlayColor,
        float durationSeconds,
        float peakStrength)
    {
        if (entity == Entity.Null ||
            !entityManager.Exists(entity) ||
            !entityManager.HasComponent<QuadOverlayPulseComponent>(entity))
        {
            return;
        }

        QuadOverlayPulseComponent pulse = new QuadOverlayPulseComponent
        {
            OverlayColor = overlayColor,
            DurationSeconds = math.max(0.01f, durationSeconds),
            RemainingSeconds = math.max(0.01f, durationSeconds),
            PeakStrength = math.clamp(peakStrength, 0f, 1f),
        };

        if (entityManager.HasComponent<QuadOverlayPulseComponent>(entity))
        {
            entityManager.SetComponentData(entity, pulse);
            entityManager.SetComponentEnabled<QuadOverlayPulseComponent>(entity, true);
        }
        else
            entityManager.AddComponentData(entity, pulse);

        ApplySpriteColor(entityManager, entity, overlayColor, pulse.PeakStrength);
    }

    public static void PlayHit(EntityManager entityManager, Entity entity)
    {
        Play(entityManager, entity, DefaultHitOverlayColor, DefaultHitOverlayDuration, DefaultHitOverlayStrength);
    }

    public static void Tick(
        EntityManager entityManager,
        Entity entity,
        ref QuadOverlayPulseComponent pulse,
        float deltaTime)
    {
        if (entity == Entity.Null || !entityManager.Exists(entity))
            return;

        float duration = math.max(0.01f, pulse.DurationSeconds);
        float remaining = math.max(0f, pulse.RemainingSeconds - math.max(0f, deltaTime));
        pulse.RemainingSeconds = remaining;

        float normalized = remaining / duration;
        float strength = math.saturate(normalized) * math.clamp(pulse.PeakStrength, 0f, 1f);
        ApplySpriteColor(entityManager, entity, pulse.OverlayColor, strength);

        if (remaining > 0f)
            return;

        ApplySpriteColor(entityManager, entity, pulse.OverlayColor, 0f);
        if (entityManager.HasComponent<QuadOverlayPulseComponent>(entity))
            entityManager.SetComponentEnabled<QuadOverlayPulseComponent>(entity, false);
    }

    private static void ApplySpriteColor(
        EntityManager entityManager,
        Entity entity,
        float4 overlayColor,
        float strength)
    {
        if (!entityManager.HasComponent<UnitAnimationComponent>(entity))
            return;

        UnitAnimationComponent animation = entityManager.GetComponentObject<UnitAnimationComponent>(entity);
        SpriteRenderer spriteRenderer = animation?.Renderer;
        if (spriteRenderer == null)
            return;

        Color overlay = new(overlayColor.x, overlayColor.y, overlayColor.z, overlayColor.w);
        spriteRenderer.color = Color.Lerp(Color.white, overlay, math.clamp(strength, 0f, 1f));
    }
}
