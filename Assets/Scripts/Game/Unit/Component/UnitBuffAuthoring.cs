using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public class UnitBuffAuthoring : MonoBehaviour
{
    class UnitBuffBaker : Baker<UnitBuffAuthoring>
    {
        public override void Bake(UnitBuffAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            UnitBuffRuntimeComponent runtimeComponent = new();
            AddComponentObject(entity, runtimeComponent);

            UnitBuffModuleData data = UnitAuthoringUtility.ResolveModuleData<UnitBuffModuleData>(authoring);
            if (data?.Buffs == null)
                return;

            for (int i = 0; i < data.Buffs.Count; i++)
            {
                UnitInitialBuffEntry entry = data.Buffs[i];
                if (entry == null || entry.BuffId < 0)
                    continue;

                runtimeComponent.Buffs.Add(new UnitBuffRuntimeEntry
                {
                    BuffId = entry.BuffId,
                    RemainingTime = entry.DurationSeconds,
                    StackCount = Mathf.Max(1, entry.StackCount),
                    HasOriginEntity = false,
                    OriginEntity = Entity.Null,
                    SourceSkillId = -1,
                });
            }
        }
    }
}
