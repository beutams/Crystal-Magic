using Unity.Entities;

public struct UnitOwnerMemberDeathEvent : IComponentData
{
    public Entity Owner;
}
