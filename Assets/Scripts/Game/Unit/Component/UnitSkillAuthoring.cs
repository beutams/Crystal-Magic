using CrystalMagic.Game.Data;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class UnitSkillAuthoring : MonoBehaviour
{
    class UnitSkillBaker : Baker<UnitSkillAuthoring>
    {
        public override void Bake(UnitSkillAuthoring authoring)
        {
            TextAsset unitDataAsset = UnitAuthoringUtility.GetUnitDataTableAsset();
            if (unitDataAsset != null)
                DependsOn(unitDataAsset);

            UnitSkillModuleData data = UnitAuthoringUtility.ResolveModuleData<UnitSkillModuleData>(authoring);
            if (data == null)
                return;

            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            UnitSkillComponent component = new();

            if (data.Skills != null)
            {
                for (int i = 0; i < data.Skills.Count; i++)
                {
                    UnitSkillSlotData slot = data.Skills[i];
                    if (slot == null || slot.SkillId < 0)
                        continue;

                    if (component.Skills.Length >= component.Skills.Capacity)
                        break;

                    component.Skills.Add(new UnitSkillEntry
                    {
                        SkillId = slot.SkillId,
                        TagMask = slot.TagMask,
                        MinDistance = math.max(0f, slot.MinDistance),
                        MaxDistance = math.max(slot.MinDistance, slot.MaxDistance),
                        CooldownSeconds = math.max(0f, slot.CooldownSeconds),
                        CooldownRemaining = 0f,
                        Weight = math.max(1, slot.Weight),
                    });
                }
            }

            AddComponent(entity, component);
        }
    }
}

public struct UnitSkillEntry
{
    public int SkillId;
    public int TagMask;
    public float MinDistance;
    public float MaxDistance;
    public float CooldownSeconds;
    public float CooldownRemaining;
    public int Weight;
    public byte IsAvailable;
}

namespace CrystalMagic.Game.Data
{
    public enum UnitSkillSelectionMode : byte
    {
        None = 0,
        RandomAll = 1,
        RandomTagMask = 2,
        ExactSkillId = 3,
    }
}

public struct UnitSkillComponent : IComponentData
{
    public FixedList512Bytes<UnitSkillEntry> Skills;
}
