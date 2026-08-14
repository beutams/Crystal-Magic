using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public class UnitManaAuthoring : MonoBehaviour
{
    class UnitManaBaker : Baker<UnitManaAuthoring>
    {
        public override void Bake(UnitManaAuthoring authoring)
        {
            TextAsset unitDataAsset = UnitAuthoringUtility.GetUnitDataTableAsset();
            if (unitDataAsset != null)
                DependsOn(unitDataAsset);

            float baseMp = 50f;
            float baseMpRegenPerSecond = 0f;
            UnitManaModuleData data = UnitAuthoringUtility.ResolveModuleData<UnitManaModuleData>(authoring);
            if (data != null)
            {
                baseMp = data.BaseMaxMp;
                baseMpRegenPerSecond = data.BaseMpRegenPerSecond;
            }

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitManaComponent
            {
                BaseMaxMp = baseMp,
                BaseMaxMpOffset = 0f,
                MpFactor = 1f,
                MpBonus = 0f,
                CurrentMana = baseMp,
                BaseMpRegenPerSecond = baseMpRegenPerSecond,
                BaseMpRegenPerSecondOffset = 0f,
                MpRegenFactor = 1f,
                MpRegenBonus = 0f,
            });
        }
    }
}

public struct UnitManaComponent : IComponentData
{
    public float BaseMaxMp;
    public float BaseMaxMpOffset;
    public float MpFactor;
    public float MpBonus;
    public float CurrentMana;
    public float BaseMpRegenPerSecond;
    public float BaseMpRegenPerSecondOffset;
    public float MpRegenFactor;
    public float MpRegenBonus;

    public float RealMaxMp => (BaseMaxMp + BaseMaxMpOffset) * MpFactor + MpBonus;
    public float RealMpRegenPerSecond => (BaseMpRegenPerSecond + BaseMpRegenPerSecondOffset) * MpRegenFactor + MpRegenBonus;
}

[UnitSourceAuthoring(typeof(UnitManaAuthoring))]
public sealed class UnitManaSource : UnitComponentSource<UnitManaComponent>
{
    protected override void Define(UnitSourceDefinitionBuilder<UnitManaComponent> builder)
    {
        builder.AddGet("unit.mana.baseMaxMp", UnitValueCategory.Number, (in UnitManaComponent value) => UnitValue.FromFloat(value.BaseMaxMp));
        builder.AddGet("unit.mana.baseMaxMpOffset", UnitValueCategory.Number, (in UnitManaComponent value) => UnitValue.FromFloat(value.BaseMaxMpOffset));
        builder.AddGet("unit.mana.mpFactor", UnitValueCategory.Number, (in UnitManaComponent value) => UnitValue.FromFloat(value.MpFactor));
        builder.AddGet("unit.mana.mpBonus", UnitValueCategory.Number, (in UnitManaComponent value) => UnitValue.FromFloat(value.MpBonus));
        builder.AddGet("unit.mana.currentMana", UnitValueCategory.Number, (in UnitManaComponent value) => UnitValue.FromFloat(value.CurrentMana));
        builder.AddGet("unit.mana.realMaxMp", UnitValueCategory.Number, (in UnitManaComponent value) => UnitValue.FromFloat(value.RealMaxMp));
        builder.AddGet("unit.mana.currentManaPercentage", UnitValueCategory.Number, (in UnitManaComponent value) => UnitValue.FromFloat(GetManaPercentage(value)));
        builder.AddGet("unit.mana.baseMpRegenPerSecond", UnitValueCategory.Number, (in UnitManaComponent value) => UnitValue.FromFloat(value.BaseMpRegenPerSecond));
        builder.AddGet("unit.mana.baseMpRegenPerSecondOffset", UnitValueCategory.Number, (in UnitManaComponent value) => UnitValue.FromFloat(value.BaseMpRegenPerSecondOffset));
        builder.AddGet("unit.mana.mpRegenFactor", UnitValueCategory.Number, (in UnitManaComponent value) => UnitValue.FromFloat(value.MpRegenFactor));
        builder.AddGet("unit.mana.mpRegenBonus", UnitValueCategory.Number, (in UnitManaComponent value) => UnitValue.FromFloat(value.MpRegenBonus));
        builder.AddGet("unit.mana.realMpRegenPerSecond", UnitValueCategory.Number, (in UnitManaComponent value) => UnitValue.FromFloat(value.RealMpRegenPerSecond));
    }

    private static float GetManaPercentage(in UnitManaComponent value)
    {
        return value.RealMaxMp > 0f ? Mathf.Clamp01(value.CurrentMana / value.RealMaxMp) : 0f;
    }
}
