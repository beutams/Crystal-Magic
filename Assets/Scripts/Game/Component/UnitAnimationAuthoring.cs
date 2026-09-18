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
            AddComponentObject(entity, UnitAnimationComponent.CreateDefault());
        }
    }
}

public sealed class UnitAnimationComponent : IComponentData
{
    public SpriteRenderer Renderer;
    public FixedString64Bytes CurrentAnimationName;
    public FixedString64Bytes PlayingAnimationName;
    public uint RequestedSequence;
    public uint PlayingSequence;
    public float RequestedStartElapsedSeconds;
    public UnitAnimationDirection LastTwoDirectionFacing;
    public float ElapsedSeconds;
    public AnimationClip CurrentAnimationClip;
    public float CurrentSampleTime;
    public Sprite CurrentSprite;

    internal static UnitAnimationComponent CreateDefault()
    {
        return new UnitAnimationComponent
        {
            Renderer = null,
            CurrentAnimationName = default,
            PlayingAnimationName = default,
            RequestedSequence = 0,
            PlayingSequence = 0,
            RequestedStartElapsedSeconds = 0f,
            LastTwoDirectionFacing = UnitAnimationDirection.Right,
            ElapsedSeconds = 0f,
            CurrentAnimationClip = null,
            CurrentSampleTime = 0f,
            CurrentSprite = null,
        };
    }
}

[UnitSourceAuthoring(typeof(UnitAnimationAuthoring))]
public sealed class UnitAnimationSource : UnitManagedComponentSource<UnitAnimationComponent>
{
    private static readonly ComparatorParameterDefinition[] s_animationNameParameter =
    {
        new ComparatorParameterDefinition("AnimationName", UnitValueCategory.String),
    };

    protected override void Define(UnitSourceDefinitionBuilder<UnitAnimationComponent> builder)
    {
        builder.AddGet("unit.animation.name", UnitValueCategory.String,
            (in UnitAnimationComponent value) => UnitValue.FromString(value?.CurrentAnimationName.ToString() ?? string.Empty));
        builder.AddContextSet("unit.animation.setName", s_animationNameParameter,
            (in UnitSourceBindingContext context, ref UnitAnimationComponent value, UnitValue[] input) =>
                SetAnimation(context, value, input, false));
        builder.AddContextSet("unit.animation.play", s_animationNameParameter,
            (in UnitSourceBindingContext context, ref UnitAnimationComponent value, UnitValue[] input) =>
                SetAnimation(context, value, input, true));
    }

    private static bool SetAnimation(
        in UnitSourceBindingContext context,
        UnitAnimationComponent animation,
        UnitValue[] input,
        bool forceRestart)
    {
        if (animation == null || input == null || input.Length != 1 ||
            !input[0].TryGetString(out string name))
        {
            return false;
        }

        FixedString64Bytes animationName = new(name.Trim());
        bool changed = !animation.CurrentAnimationName.Equals(animationName);
        bool hasState = context.EntityManager.HasComponent<UnitAnimationStateComponent>(context.Entity);
        if (!hasState)
            return false;

        if (!changed && !forceRestart && hasState)
            return true;

        UnitAnimationStateComponent state =
            context.EntityManager.GetComponentData<UnitAnimationStateComponent>(context.Entity);
        uint sequence = System.Math.Max(animation.RequestedSequence, state.Sequence) + 1u;
        if (sequence == 0u)
            sequence = 1u;

        uint startFrame = FrameManagerUtility.TryGet(context.EntityManager, out FrameManager frameManager)
            ? frameManager.currentFrame
            : 0u;
        state.AnimationName = animationName;
        state.StartFrame = startFrame;
        state.Sequence = sequence;
        state.NetworkDirty = 1;
        context.EntityManager.SetComponentData(context.Entity, state);

        animation.CurrentAnimationName = animationName;
        animation.RequestedSequence = sequence;
        animation.RequestedStartElapsedSeconds = 0f;
        if (forceRestart)
        {
            animation.PlayingAnimationName = default;
            animation.PlayingSequence = 0u;
            animation.ElapsedSeconds = 0f;
        }

        return true;
    }
}
