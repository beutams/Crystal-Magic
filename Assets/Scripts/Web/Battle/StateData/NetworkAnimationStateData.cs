using System;
using Unity.Collections;
using Unity.Entities;

[Serializable]
public sealed class NetworkAnimationStateData : NetworkStateData
{
    public string animationName;
    public uint startFrame;
    public uint sequence;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.TryGetEntity(unitId, out Entity entity))
            return;

        FixedString64Bytes fixedName = new(animationName ?? string.Empty);
        UnitAnimationComponent animation = context.EntityManager.HasComponent<UnitAnimationComponent>(entity)
            ? context.EntityManager.GetComponentData<UnitAnimationComponent>(entity)
            : UnitAnimationComponent.CreateDefault();
        bool requestChanged = !animation.AnimationName.Equals(fixedName) || animation.Sequence != sequence;
        animation.AnimationName = fixedName;
        animation.StartFrame = startFrame;
        animation.Sequence = sequence;
        animation.NetworkDirty = 0;
        if (requestChanged)
            animation.RequestedStartElapsedSeconds = context.GetElapsedSeconds(startFrame);

        context.SetOrAdd(entity, animation);
    }
}
