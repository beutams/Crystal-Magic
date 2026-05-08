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
    //移动
    public float2 MoveDirection;
    //技能释放
    public bool WantToCast;
    public float2 CastTargetPosition;
}
