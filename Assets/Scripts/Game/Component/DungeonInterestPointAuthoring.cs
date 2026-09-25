using Unity.Entities;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DungeonInterestPointAuthoring : MonoBehaviour
{
    [SerializeField] private float _spawnDistance = 18f;
    [SerializeField] private float _patrolSpeed = 3f;
    [SerializeField] private float _arrivalDistance = 0.75f;

    private sealed class Baker : Baker<DungeonInterestPointAuthoring>
    {
        public override void Bake(DungeonInterestPointAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new DungeonInterestPointComponent
            {
                SpawnDistance = Mathf.Max(0f, authoring._spawnDistance),
                PatrolSpeed = Mathf.Max(0f, authoring._patrolSpeed),
                ArrivalDistance = Mathf.Max(0.05f, authoring._arrivalDistance),
                PatrolEnabled = 1,
            });
            AddComponent(entity, new UnitFactionComponent
            {
                Value = UnitFactionType.Interactable,
            });
        }
    }
}
