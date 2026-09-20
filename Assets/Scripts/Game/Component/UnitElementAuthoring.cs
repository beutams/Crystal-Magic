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

[UnitSourceProvider(typeof(UnitElementComponent), typeof(UnitElementAuthoring))]
public static class UnitElementSource
{
    [UnitSourceGet(0, "unit.element.waterPower", UnitValueCategory.Number)]
    [UnitSourceGet(1, "unit.element.firePower", UnitValueCategory.Number)]
    [UnitSourceGet(2, "unit.element.lightningPower", UnitValueCategory.Number)]
    [UnitSourceGet(3, "unit.element.windPower", UnitValueCategory.Number)]
    public static bool TryGet(
        int operation,
        Entity entity,
        in ComponentLookup<UnitElementComponent> elements,
        in ComponentLookup<UnitModifierComponent> modifiers,
        in UnitSourceArguments arguments,
        out UnitSourceValue result)
    {
        if (!elements.TryGetComponent(entity, out UnitElementComponent elementValue))
        {
            result = default;
            return false;
        }
        UnitModifierComponent modifier = modifiers.TryGetComponent(
            entity,
            out UnitModifierComponent resolvedModifier)
            ? resolvedModifier
            : UnitModifierComponent.CreateIdentity();

        CrystalMagic.Game.Data.Effects.ElementType element = operation switch
        {
            0 => CrystalMagic.Game.Data.Effects.ElementType.Water,
            1 => CrystalMagic.Game.Data.Effects.ElementType.Fire,
            2 => CrystalMagic.Game.Data.Effects.ElementType.Lightning,
            3 => CrystalMagic.Game.Data.Effects.ElementType.Wind,
            _ => CrystalMagic.Game.Data.Effects.ElementType.None,
        };
        if (element == CrystalMagic.Game.Data.Effects.ElementType.None)
        {
            result = default;
            return false;
        }

        result = UnitSourceValue.FromFloat(
            UnitModifierResolver.GetElementPower(in elementValue, in modifier, element));
        return true;
    }
}
