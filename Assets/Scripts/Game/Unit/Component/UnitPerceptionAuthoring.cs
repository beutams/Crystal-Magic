using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class UnitPerceptionAuthoring : MonoBehaviour
{
    [SerializeField] private float _searchRadius = 8f;

    public float SearchRadius
    {
        get => _searchRadius;
        set => _searchRadius = Mathf.Max(0f, value);
    }

    class UnitPerceptionBaker : Baker<UnitPerceptionAuthoring>
    {
        public override void Bake(UnitPerceptionAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitPerceptionComponent
            {
                SearchRadius = Mathf.Max(0f, authoring.SearchRadius),
                HasTarget = false,
                TargetEntity = Entity.Null,
                TargetPosition = float2.zero,
                TargetDistance = 0f,
            });
        }
    }
}

public struct UnitPerceptionComponent : IComponentData
{
    public float SearchRadius;
    public bool HasTarget;
    public Entity TargetEntity;
    public float2 TargetPosition;
    public float TargetDistance;
}
