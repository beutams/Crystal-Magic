using Unity.Entities;

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
