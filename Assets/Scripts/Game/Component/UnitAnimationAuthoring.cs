using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public sealed class UnitAnimationAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<UnitAnimationAuthoring>
    {
        public override void Bake(UnitAnimationAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, UnitAnimationComponent.CreateDefault());
        }
    }
}

public struct UnitAnimationComponent : IComponentData
{
    public FixedString64Bytes AnimationName;
    public uint StartFrame;
    public uint Sequence;
    public byte NetworkDirty;
    public FixedString64Bytes PlayingAnimationName;
    public uint PlayingSequence;
    public float RequestedStartElapsedSeconds;
    public UnitAnimationDirection LastTwoDirectionFacing;
    public float ElapsedSeconds;
    public float CurrentSampleTime;
    public float CurrentClipLength;

    internal static UnitAnimationComponent CreateDefault()
    {
        return new UnitAnimationComponent
        {
            LastTwoDirectionFacing = UnitAnimationDirection.Right,
        };
    }
}

[UnitSourceProvider(typeof(UnitAnimationComponent), typeof(UnitAnimationAuthoring))]
public static class UnitAnimationSource
{
    [UnitSourceGet(0, "unit.animation.name", UnitValueCategory.String)]
    public static bool TryGet(
        int operation,
        in UnitAnimationComponent value,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        result = operation == 0 ? UnitSourceValue.FromString(value.AnimationName) : UnitSourceValue.None;
        return result.Type != UnitValueType.None;
    }

    [UnitSourceSet(0, "unit.animation.setName", UnitValueCategory.String,
        ParameterNames = new[] { "AnimationName" })]
    [UnitSourceSet(1, "unit.animation.play", UnitValueCategory.String,
        ParameterNames = new[] { "AnimationName" })]
    public static bool TrySet(
        int operation,
        Entity entity,
        ref ComponentLookup<UnitAnimationComponent> animationLookup,
        in UnitSourceArguments arguments)
    {
        if ((operation != 0 && operation != 1) ||
            !arguments.TryGetString(0, out FixedString128Bytes sourceName) ||
            !animationLookup.HasComponent(entity))
        {
            return false;
        }

        FixedString128Bytes trimmedSourceName = sourceName.Trim();
        FixedString64Bytes animationName = default;
        if (animationName.CopyFrom(in trimmedSourceName) != CopyError.None)
            return false;

        UnitAnimationComponent animation = animationLookup[entity];
        bool forceRestart = operation == 1;
        bool changed = !animation.AnimationName.Equals(animationName);
        if (!changed && !forceRestart)
            return true;

        uint sequence = animation.Sequence + 1u;
        if (sequence == 0u)
            sequence = 1u;

        animation.AnimationName = animationName;
        animation.Sequence = sequence;
        animation.NetworkDirty = 1;
        animation.RequestedStartElapsedSeconds = 0f;
        if (forceRestart)
        {
            animation.PlayingAnimationName = default;
            animation.PlayingSequence = 0u;
            animation.ElapsedSeconds = 0f;
        }

        animationLookup[entity] = animation;
        return true;
    }
}
