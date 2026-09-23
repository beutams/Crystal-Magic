using Unity.Entities;

public struct DungeonMonsterSpawnComponent : IComponentData
{
    public int SaveId;
    public int RegionId;
    public int SquadId;
    public byte IsBoss;
    public byte CountsAsGuard;
    public byte CountsAsPatrol;
}
