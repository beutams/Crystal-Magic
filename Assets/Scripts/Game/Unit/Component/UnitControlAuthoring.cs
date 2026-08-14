using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public sealed class UnitControlAuthoring : MonoBehaviour
{
    private sealed class Baker : Baker<UnitControlAuthoring>
    {
        public override void Bake(UnitControlAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new UnitControlRuntimeComponent
            {
                Entries = new FixedList512Bytes<UnitControlRuntimeEntry>(),
                ActiveType = UnitControlType.None,
                ActiveRemainingTime = 0f,
                ActivePriority = 0,
                LockMove = 0,
                LockCast = 0,
                HasControl = 0,
                ActiveSourceEntity = Entity.Null,
                ActiveMotionVelocity = float2.zero,
                ActiveMotionDamping = 0f,
            });
        }
    }
}
