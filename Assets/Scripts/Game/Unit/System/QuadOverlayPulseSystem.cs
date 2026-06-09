using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitAnimationSystem))]
[UpdateAfter(typeof(QuadAnimationSystem))]
[UpdateBefore(typeof(DestroyEntitySystem))]
public partial class QuadOverlayPulseSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRW<QuadOverlayPulseComponent> pulse, Entity entity) in
                 SystemAPI.Query<RefRW<QuadOverlayPulseComponent>>().WithEntityAccess())
        {
            QuadOverlayPulseUtility.Tick(EntityManager, entity, ref pulse.ValueRW, deltaTime);
        }
    }
}

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
            !entityManager.HasComponent<Unity.Rendering.MaterialMeshInfo>(entity))
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

        ApplyOverlayProperties(entityManager, entity, overlayColor, pulse.PeakStrength);
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
        ApplyOverlayProperties(entityManager, entity, pulse.OverlayColor, strength);

        if (remaining > 0f)
            return;

        if (entityManager.HasComponent<QuadOverlayPulseComponent>(entity))
            entityManager.SetComponentEnabled<QuadOverlayPulseComponent>(entity, false);
    }

    private static void ApplyOverlayProperties(
        EntityManager entityManager,
        Entity entity,
        float4 overlayColor,
        float strength)
    {
        SetOrAddProperty(entityManager, entity, new UnitAnimationOverlayColorProperty
        {
            Value = overlayColor,
        });
        SetOrAddProperty(entityManager, entity, new UnitAnimationOverlayStrengthProperty
        {
            Value = new float4(math.clamp(strength, 0f, 1f), 0f, 0f, 0f),
        });
    }

    private static void SetOrAddProperty<T>(EntityManager entityManager, Entity entity, T value)
        where T : unmanaged, IComponentData
    {
        if (entityManager.HasComponent<T>(entity))
            entityManager.SetComponentData(entity, value);
        else
            entityManager.AddComponentData(entity, value);
    }
}
