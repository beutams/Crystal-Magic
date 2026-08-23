using Unity.Entities;
using UnityEngine;

public sealed class DungeonTreasureAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<DungeonTreasureAuthoring>
    {
        public override void Bake(DungeonTreasureAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new TreasureComponent
            {
                RegionId = -1,
                RandomSeed = 1u,
                InterestSize = 0,
                IsOpened = 0,
            });
            AddComponent(entity, new UnitInteractableComponent
            {
                Data = new UnitInteractionData
                {
                    Kind = InteractionKind.Treasure,
                    DataId = -1,
                },
                RangeSq = -1f,
                IsEnabled = 1,
            });
            AddBuffer<DungeonTreasureCandidateItemElement>(entity);
        }
    }
}

public struct TreasureComponent : IComponentData
{
    public int RegionId;
    public uint RandomSeed;
    public byte InterestSize;
    public byte IsOpened;
}
