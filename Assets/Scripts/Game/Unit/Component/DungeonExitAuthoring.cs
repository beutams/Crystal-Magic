using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public sealed class DungeonExitAuthoring : MonoBehaviour
{
    [SerializeField, HideInInspector] private float _interactionRange = 2.5f;

    public float InteractionRange
    {
        get => _interactionRange;
        set => _interactionRange = value;
    }

    private sealed class Baker : Baker<DungeonExitAuthoring>
    {
        public override void Bake(DungeonExitAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new DungeonExitComponent
            {
                RegionId = -1,
                TargetFloor = 1,
                InteractionRange = Mathf.Max(0.5f, authoring.InteractionRange),
                RequiresRoomClear = 1,
                IsOpen = 0,
                ClosedMaterialPath = FixedString128Bytes.Empty,
                OpenMaterialPath = FixedString128Bytes.Empty,
            });
        }
    }
}

public struct DungeonExitComponent : IComponentData
{
    public int RegionId;
    public int TargetFloor;
    public float InteractionRange;
    public byte RequiresRoomClear;
    public byte IsOpen;
    public FixedString128Bytes ClosedMaterialPath;
    public FixedString128Bytes OpenMaterialPath;
}
