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
            AddComponentObject(entity, UnitAnimationComponent.CreateDefault());
        }
    }
}

public sealed class UnitAnimationComponent : IComponentData
{
    public SpriteRenderer Renderer;
    public FixedString64Bytes CurrentAnimationName;
    public FixedString64Bytes PlayingAnimationName;
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
    protected override void Define(UnitSourceDefinitionBuilder<UnitAnimationComponent> builder)
    {
        builder.AddGet("unit.animation.name", UnitValueCategory.String,
            (in UnitAnimationComponent value) => UnitValue.FromString(value?.CurrentAnimationName.ToString() ?? string.Empty));
        builder.AddSet("unit.animation.setName", UnitValueCategory.String,
            (ref UnitAnimationComponent value, UnitValue input) =>
            {
                if (value == null || !input.TryGetString(out string name))
                    return false;

                value.CurrentAnimationName = new FixedString64Bytes(name.Trim());
                return true;
            });
    }
}
