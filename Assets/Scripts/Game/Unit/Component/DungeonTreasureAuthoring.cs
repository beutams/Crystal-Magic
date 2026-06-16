using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

public sealed class DungeonTreasureAuthoring : MonoBehaviour
{
    [SerializeField, HideInInspector] private float _interactionRange = 1.35f;

    public float InteractionRange
    {
        get => _interactionRange;
        set => _interactionRange = value;
    }

    private sealed class Baker : Baker<DungeonTreasureAuthoring>
    {
        public override void Bake(DungeonTreasureAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new DungeonTreasureComponent
            {
                RegionId = -1,
                InteractionRange = Mathf.Max(0.25f, authoring.InteractionRange),
                IsOpened = 0,
            });
            AddBuffer<DungeonTreasureRewardElement>(entity);
        }
    }
}

public struct DungeonTreasureComponent : IComponentData
{
    public int RegionId;
    public float InteractionRange;
    public byte IsOpened;
}
