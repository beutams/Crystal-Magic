using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public class UnitManaAuthoring : MonoBehaviour
{
    public string UnitName;

    class UnitManaBaker : Baker<UnitManaAuthoring>
    {
        public override void Bake(UnitManaAuthoring authoring)
        {
            float baseMp = 50f;
            if (!string.IsNullOrEmpty(authoring.UnitName))
            {
                UnitData data = EditorComponents.Data.Find<UnitData>(r => r.Name == authoring.UnitName);
                if (data != null) baseMp = data.BaseMaxMp;
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

/// <summary>
/// 法力组件——有此组件即为有蓝量的单位。
/// </summary>
public struct UnitManaComponent : IComponentData
{
    public float BaseMaxMp;
    public float MpFactor;
    public float MpBonus;
    public float CurrentMana;

    public float RealMaxMp => BaseMaxMp * MpFactor + MpBonus;
}
