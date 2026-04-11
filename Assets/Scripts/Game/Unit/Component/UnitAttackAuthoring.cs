using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public class UnitAttackAuthoring : MonoBehaviour
{
    public string UnitName;

    class UnitAttackBaker : Baker<UnitAttackAuthoring>
    {
        public override void Bake(UnitAttackAuthoring authoring)
        {
            float baseAttack = 10f;
            float baseRange  = 1f;
            if (!string.IsNullOrEmpty(authoring.UnitName))
            {
                UnitData data = EditorComponents.Data.Find<UnitData>(r => r.Name == authoring.UnitName);
                if (data != null)
                {
                    baseAttack = data.BaseAttackPower;
                    baseRange  = data.BaseSkillRange;
                }
            }

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitAttackComponent
            {
                BaseAttackPower = baseAttack,
                AttackFactor    = 1f,
                AttackBonus     = 0f,
                BaseSkillRange  = baseRange,
                RangeFactor     = 1f,
                RangeBonus      = 0f,
            });
        }
    }
}

/// <summary>
/// 攻击组件——有此组件即为可攻击单位。
/// </summary>
public struct UnitAttackComponent : IComponentData
{
    public float BaseAttackPower;
    public float AttackFactor;
    public float AttackBonus;
    public float BaseSkillRange;
    public float RangeFactor;
    public float RangeBonus;

    public float RealAttackPower => BaseAttackPower * AttackFactor + AttackBonus;
    public float RealSkillRange  => BaseSkillRange  * RangeFactor  + RangeBonus;
}
