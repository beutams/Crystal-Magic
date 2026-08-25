using Unity.Entities;

[UpdateInGroup(typeof(UnitExecutionSystemGroup))]
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
