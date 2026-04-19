using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public class UnitManaAuthoring : MonoBehaviour
{
    class UnitManaBaker : Baker<UnitManaAuthoring>
    {
        public override void Bake(UnitManaAuthoring authoring)
        {
            float baseMp = 50f;
            UnitData data = UnitAuthoringUtility.ResolveUnitData(authoring);
            if (data != null)
            {
                baseMp = data.BaseMaxMp;
            }

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitManaComponent
            {
                BaseMaxMp    = baseMp,
                MpFactor     = 1f,
                MpBonus      = 0f,
                CurrentMana  = baseMp,
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

    public float RealMaxMp => BaseMaxMp * MpFactor + MpBonus;
}

/// <summary>
/// 娉曞姏缁勪欢鈥斺€旀湁姝ょ粍浠跺嵆涓烘湁钃濋噺鐨勫崟浣嶃€?
/// </summary>
