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
            SpriteRenderer spriteRenderer = authoring.GetComponent<SpriteRenderer>();
            AddComponentObject(entity, UnitAnimationComponent.CreateDefault(spriteRenderer));
        }
    }
}

public sealed class UnitAnimationComponent : IComponentData
{
    internal SpriteRenderer Renderer;
    internal FixedString64Bytes Name;
    internal FixedString64Bytes PlayingName;
    internal float ElapsedSeconds;

    internal static UnitAnimationComponent CreateDefault(SpriteRenderer renderer)
    {
        return new UnitAnimationComponent
        {
            Renderer = renderer,
            Name = default,
            PlayingName = default,
            ElapsedSeconds = 0f,
        };
    }
}

[UnitSourceAuthoring(typeof(UnitAnimationAuthoring))]
public sealed class UnitAnimationSource : UnitManagedComponentSource<UnitAnimationComponent>
{
    protected override void Define(UnitSourceDefinitionBuilder<UnitAnimationComponent> builder)
    {
        builder.AddGet("unit.animation.name", UnitValueCategory.String,
            (in UnitAnimationComponent value) => UnitValue.FromString(value?.Name.ToString() ?? string.Empty));
        builder.AddSet("unit.animation.setName", UnitValueCategory.String,
            (ref UnitAnimationComponent value, UnitValue input) =>
            {
                if (value == null || !input.TryGetString(out string name))
                    return false;

                value.Name = new FixedString64Bytes(name.Trim());
                return true;
            });
    }
}
