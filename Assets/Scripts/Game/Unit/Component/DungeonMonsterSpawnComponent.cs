using Unity.Entities;

public struct DungeonMonsterSpawnComponent : IComponentData
{
    public int RegionId;
    public int SquadId;
    public byte IsBoss;
}
