using Unity.Entities;
using UnityEngine;

public sealed class UnitElementAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<UnitElementAuthoring>
    {
        public override void Bake(UnitElementAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitElementComponent
            {
                WaterPower = 0f,
                FirePower = 0f,
                LightningPower = 0f,
                WindPower = 0f,
            });
        }
    }
}

public struct UnitElementComponent : IComponentData
{
    public float WaterPower;
    public float FirePower;
    public float LightningPower;
    public float WindPower;

    public float GetPowerBonus(CrystalMagic.Game.Data.Effects.ElementType elementType)
    {
        return elementType switch
        {
            CrystalMagic.Game.Data.Effects.ElementType.Water => WaterPower,
            CrystalMagic.Game.Data.Effects.ElementType.Fire => FirePower,
            CrystalMagic.Game.Data.Effects.ElementType.Lightning => LightningPower,
            CrystalMagic.Game.Data.Effects.ElementType.Wind => WindPower,
            _ => 0f,
        };
    }
}

[UnitSourceAuthoring(typeof(UnitElementAuthoring))]
public sealed class UnitElementSource : UnitComponentSource<UnitElementComponent>
{
    private static readonly ComparatorParameterDefinition[] s_noParameters = System.Array.Empty<ComparatorParameterDefinition>();

    protected override void Define(UnitSourceDefinitionBuilder<UnitElementComponent> builder)
    {
        builder.AddContextGet("unit.element.waterPower", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in UnitElementComponent _, UnitValue[] _) =>
                UnitValue.FromFloat(UnitModifierResolver.GetElementPower(context.EntityManager, context.Entity, CrystalMagic.Game.Data.Effects.ElementType.Water)));
        builder.AddContextGet("unit.element.firePower", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in UnitElementComponent _, UnitValue[] _) =>
                UnitValue.FromFloat(UnitModifierResolver.GetElementPower(context.EntityManager, context.Entity, CrystalMagic.Game.Data.Effects.ElementType.Fire)));
        builder.AddContextGet("unit.element.lightningPower", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in UnitElementComponent _, UnitValue[] _) =>
                UnitValue.FromFloat(UnitModifierResolver.GetElementPower(context.EntityManager, context.Entity, CrystalMagic.Game.Data.Effects.ElementType.Lightning)));
        builder.AddContextGet("unit.element.windPower", UnitValueCategory.Number, s_noParameters,
            (in UnitSourceBindingContext context, in UnitElementComponent _, UnitValue[] _) =>
                UnitValue.FromFloat(UnitModifierResolver.GetElementPower(context.EntityManager, context.Entity, CrystalMagic.Game.Data.Effects.ElementType.Wind)));
    }
}
