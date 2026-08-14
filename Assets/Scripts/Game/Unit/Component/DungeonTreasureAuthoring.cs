using Unity.Entities;
using UnityEngine;

public sealed class DungeonTreasureAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<DungeonTreasureAuthoring>
    {
        public override void Bake(DungeonTreasureAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new DungeonTreasureComponent
            {
                RegionId = -1,
                RandomSeed = 1u,
                InterestSize = 0,
                IsOpened = 0,
            });
            AddBuffer<DungeonTreasureCandidateItemElement>(entity);
        }
    }
}

public struct DungeonTreasureComponent : IComponentData
{
    public int RegionId;
    public uint RandomSeed;
    public byte InterestSize;
    public byte IsOpened;
}