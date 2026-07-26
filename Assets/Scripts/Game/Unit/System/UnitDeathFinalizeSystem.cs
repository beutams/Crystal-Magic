using Unity.Entities;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
[UpdateAfter(typeof(UnitAnimationSystem))]
partial class UnitDeathFinalizeSystem : SystemBase
{
    private const float MissingDeathAnimationDelaySeconds = 0.1f;

    protected override void OnUpdate()
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        foreach ((RefRW<UnitDeathComponent> deathRef,
                  RefRO<UnitAnimationComponent> animationRef,
                  Entity entity) in
                 SystemAPI.Query<RefRW<UnitDeathComponent>, RefRO<UnitAnimationComponent>>()
                     .WithEntityAccess())
        {
            UnitDeathComponent death = deathRef.ValueRW;
            if (death.Phase != UnitDeathPhase.PlayingAnimation)
                continue;

            death.ElapsedSeconds += deltaTime;
            bool animationFinished = animationRef.ValueRO.IsCurrentClipFinished != 0;
            bool invalidDeathAnimation =
                (animationRef.ValueRO.ClipId < 0 || animationRef.ValueRO.IsCurrentClipLooping != 0) &&
                death.ElapsedSeconds >= MissingDeathAnimationDelaySeconds;
            if (!animationFinished && !invalidDeathAnimation)
            {
                deathRef.ValueRW = death;
                continue;
            }

            if (invalidDeathAnimation)
            {
                UnityEngine.Debug.LogError(
                    $"[UnitDeathFinalizeSystem] {entity} has no valid non-looping DeathState animation clip. " +
                    "Finishing death to prevent a blocked unit.");
            }

            death.Phase = UnitDeathPhase.Completed;
            deathRef.ValueRW = death;

            // The dungeon flow consumes the completed player death before its scene unloads.
            if (EntityManager.HasComponent<PlayerTag>(entity))
                continue;

            EntityManager.SetComponentEnabled<DestroyEntityFlag>(entity, true);
        }
    }
}
