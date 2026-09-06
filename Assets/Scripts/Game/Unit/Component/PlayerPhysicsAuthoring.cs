using Unity.Entities;
using Unity.Physics;
using UnityEngine;

public sealed class PlayerPhysicsAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<PlayerPhysicsAuthoring>
    {
        public override void Bake(PlayerPhysicsAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new PhysicsMassOverride
            {
                IsKinematic = 0,
                SetVelocityToZero = 0,
            });
        }
    }
}
