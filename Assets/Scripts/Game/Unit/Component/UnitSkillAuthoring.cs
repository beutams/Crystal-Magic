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
            UnitSkillComponent component = new UnitSkillComponent
            {
                RequestMode = UnitSkillSelectionMode.None,
                RequestedSkillId = 0,
                RequestedTagMask = 0,
                HasPendingCast = false,
                PendingSkillIndex = -1,
                HasLockedTarget = false,
                LockedTargetPosition = float2.zero,
            };

            if (data.Skills != null)
            {
                for (int i = 0; i < data.Skills.Count; i++)
                {
                    UnitSkillSlotData slot = data.Skills[i];
                    if (slot == null || slot.SkillId <= 0)
                        continue;

                    if (component.Skills.Length >= component.Skills.Capacity)
                        break;

                    component.Skills.Add(new UnitSkillEntry
                    {
                        SkillId = slot.SkillId,
                        SkillEffectId = slot.SkillEffectId,
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
    public int SkillEffectId;
    public int TagMask;
    public float MinDistance;
    public float MaxDistance;
    public float CooldownSeconds;
    public float CooldownRemaining;
    public int Weight;
}

public struct UnitSkillComponent : IComponentData
{
    public FixedList512Bytes<UnitSkillEntry> Skills;
    public UnitSkillSelectionMode RequestMode;
    public int RequestedSkillId;
    public int RequestedTagMask;
    public bool HasPendingCast;
    public int PendingSkillIndex;
    public bool HasLockedTarget;
    public float2 LockedTargetPosition;

    public void ClearPending()
    {
        HasPendingCast = false;
        PendingSkillIndex = -1;
        HasLockedTarget = false;
        LockedTargetPosition = float2.zero;
    }

    public void ClearRequest()
    {
        RequestMode = UnitSkillSelectionMode.None;
        RequestedSkillId = 0;
        RequestedTagMask = 0;
    }
}
