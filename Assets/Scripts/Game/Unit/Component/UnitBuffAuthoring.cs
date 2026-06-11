using CrystalMagic.Game.Data;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class UnitBuffAuthoring : MonoBehaviour
{
    class UnitBuffBaker : Baker<UnitBuffAuthoring>
    {
        public override void Bake(UnitBuffAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            DynamicBuffer<UnitBuffElement> buffer = AddBuffer<UnitBuffElement>(entity);
            AddComponentObject(entity, new UnitBuffPayloadComponent());

            UnitBuffModuleData data = UnitAuthoringUtility.ResolveModuleData<UnitBuffModuleData>(authoring);
            if (data?.Buffs == null)
                return;

            for (int i = 0; i < data.Buffs.Count; i++)
            {
                UnitInitialBuffEntry entry = data.Buffs[i];
                if (entry == null || entry.BuffId < 0)
                    continue;

                buffer.Add(new UnitBuffElement
                {
                    BuffId = entry.BuffId,
                    RemainingTime = entry.DurationSeconds < 0f ? -1f : math.max(0f, entry.DurationSeconds),
                    NextTickTime = 0f,
                    StackCount = math.max(1, entry.StackCount),
                    RuntimePayloadId = -1,
                    HasOriginEntity = 0,
                    OriginEntity = Entity.Null,
                    SourceSkillId = -1,
                    SourceExecutionToken = 0,
                    ConsumeOnDamageTaken = 0,
                    RemainingTriggerCount = 0,
                });
            }
        }
    }
}

public struct UnitBuffElement : IBufferElementData
{
    public int BuffId;
    public float RemainingTime;
    public float NextTickTime;
    public int StackCount;
    public int RuntimePayloadId;
    public byte HasOriginEntity;
    public Entity OriginEntity;
    public int SourceSkillId;
    public int SourceExecutionToken;
    public byte ConsumeOnDamageTaken;
    public int RemainingTriggerCount;
}
