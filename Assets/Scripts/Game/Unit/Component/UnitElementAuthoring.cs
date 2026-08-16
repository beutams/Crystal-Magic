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
    protected override void Define(UnitSourceDefinitionBuilder<UnitElementComponent> builder)
    {
        builder.AddGet("unit.element.waterPower", UnitValueCategory.Number, (in UnitElementComponent value) => UnitValue.FromFloat(value.WaterPower));
        builder.AddGet("unit.element.firePower", UnitValueCategory.Number, (in UnitElementComponent value) => UnitValue.FromFloat(value.FirePower));
        builder.AddGet("unit.element.lightningPower", UnitValueCategory.Number, (in UnitElementComponent value) => UnitValue.FromFloat(value.LightningPower));
        builder.AddGet("unit.element.windPower", UnitValueCategory.Number, (in UnitElementComponent value) => UnitValue.FromFloat(value.WindPower));
    }
}
