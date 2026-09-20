using Unity.Entities;
using UnityEngine;

[RequireComponent(typeof(PlayerCurrentSkillAuthoring))]
public sealed class PlayerInputAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<PlayerInputAuthoring>
    {
        public override void Bake(PlayerInputAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<PlayerInputComponent>(entity);
        }
    }
}
