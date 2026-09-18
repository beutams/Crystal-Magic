using CrystalMagic.Game.Data;
using Server;
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
        EntityManager entityManager,
        Entity entity,
        in UnitSourceArguments arguments)
    {
        if ((operation != 0 && operation != 1) ||
            !arguments.TryGetString(0, out FixedString128Bytes sourceName) ||
            !entityManager.Exists(entity) || !entityManager.HasComponent<UnitAnimationComponent>(entity))
        {
            return false;
        }

        FixedString64Bytes animationName = new(sourceName.ToString().Trim());
        UnitAnimationComponent animation = entityManager.GetComponentData<UnitAnimationComponent>(entity);
        bool forceRestart = operation == 1;
        bool changed = !animation.AnimationName.Equals(animationName);
        if (!changed && !forceRestart)
            return true;

        uint sequence = animation.Sequence + 1u;
        if (sequence == 0u)
            sequence = 1u;

        uint startFrame = FrameManagerUtility.TryGet(entityManager, out FrameManager frameManager)
            ? frameManager.currentFrame
            : 0u;

        animation.AnimationName = animationName;
        animation.StartFrame = startFrame;
        animation.Sequence = sequence;
        animation.NetworkDirty = 1;
        animation.RequestedStartElapsedSeconds = 0f;
        if (forceRestart)
        {
            animation.PlayingAnimationName = default;
            animation.PlayingSequence = 0u;
            animation.ElapsedSeconds = 0f;
        }

        entityManager.SetComponentData(entity, animation);
        return true;
    }
}
