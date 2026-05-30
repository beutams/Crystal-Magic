using Unity.Entities;
using UnityEngine;

public class UnitBuffAuthoring : MonoBehaviour
{
    class UnitBuffBaker : Baker<UnitBuffAuthoring>
    {
        public override void Bake(UnitBuffAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            AddBuffer<UnitBuffElement>(entity);
            AddComponentObject(entity, new UnitBuffPayloadComponent());
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
