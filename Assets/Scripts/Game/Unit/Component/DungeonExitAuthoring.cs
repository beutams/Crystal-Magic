using Unity.Entities;
using UnityEngine;

public sealed class DungeonExitAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<DungeonExitAuthoring>
    {
        public override void Bake(DungeonExitAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new DungeonExitComponent
            {
                RegionId = -1,
                TargetFloor = 1,
                RequiresRoomClear = 1,
                IsOpen = 0,
            });
        }
    }
}

public struct DungeonExitComponent : IComponentData
{
    public int RegionId;
    public int TargetFloor;
    public byte RequiresRoomClear;
    public byte IsOpen;
}
