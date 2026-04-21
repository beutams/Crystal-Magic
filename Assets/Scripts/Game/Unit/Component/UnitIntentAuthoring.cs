using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class UnitIntentAuthoring : MonoBehaviour
{
    class UnitIntentBaker : Baker<UnitIntentAuthoring>
    {
        public override void Bake(UnitIntentAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitIntentComponent());
        }
    }
}

public struct UnitIntentComponent : IComponentData
{
    public float2 MoveDirection;
    public bool WantToCast;
    public bool HasCastTarget;
    public float2 CastTargetPosition;
}
