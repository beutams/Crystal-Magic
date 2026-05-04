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
                MpFactor = 1f,
                MpBonus = 0f,
                CurrentMana = baseMp,
                BaseMpRegenPerSecond = baseMpRegenPerSecond,
                MpRegenFactor = 1f,
                MpRegenBonus = 0f,
            });
        }
    }
}

public struct UnitManaComponent : IComponentData
{
    public float BaseMaxMp;
    public float MpFactor;
    public float MpBonus;
    public float CurrentMana;
    public float BaseMpRegenPerSecond;
    public float MpRegenFactor;
    public float MpRegenBonus;

    public float RealMaxMp => BaseMaxMp * MpFactor + MpBonus;
    public float RealMpRegenPerSecond => BaseMpRegenPerSecond * MpRegenFactor + MpRegenBonus;
}
