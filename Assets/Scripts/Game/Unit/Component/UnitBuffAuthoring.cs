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
        }
    }
}

public struct UnitBuffElement : IBufferElementData
{
    public int BuffId;
    public float RemainingTime;
    public int StackCount;
}