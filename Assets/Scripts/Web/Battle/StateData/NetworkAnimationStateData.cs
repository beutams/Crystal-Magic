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
        context.SetOrAdd(entity, new UnitAnimationStateComponent
        {
            AnimationName = fixedName,
            StartFrame = startFrame,
            Sequence = sequence,
            NetworkDirty = 0,
        });

        if (!context.EntityManager.HasComponent<UnitAnimationComponent>(entity))
            return;

        UnitAnimationComponent animation = context.EntityManager.GetComponentObject<UnitAnimationComponent>(entity);
        if (animation == null)
            return;

        animation.CurrentAnimationName = fixedName;
        animation.RequestedSequence = sequence;
        animation.RequestedStartElapsedSeconds = context.GetElapsedSeconds(startFrame);
    }
}
