using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class UnitCastAvailabilityAuthoring : MonoBehaviour
{
    class UnitCastAvailabilityBaker : Baker<UnitCastAvailabilityAuthoring>
    {
        public override void Bake(UnitCastAvailabilityAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, UnitCastAvailabilityComponent.CreateDefault());
        }
    }
}

public struct UnitCastAvailabilityComponent : IComponentData
{
    public byte CanStartCast;
    public FixedList128Bytes<int> CastableSkillIndices;

    public static UnitCastAvailabilityComponent CreateDefault()
    {
        return new UnitCastAvailabilityComponent
        {
            CanStartCast = 0,
            CastableSkillIndices = default,
        };
    }
}
