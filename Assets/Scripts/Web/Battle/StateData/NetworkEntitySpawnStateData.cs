using System;
using Server;
using Unity.Entities;

[Serializable]
public sealed class NetworkEntitySpawnStateData : NetworkStateData
{
    public NetworkEntitySpawnInfo entityInfo;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (entityInfo == null ||
            entityInfo.unitId == Guid.Empty ||
            string.IsNullOrWhiteSpace(entityInfo.prefabName) ||
            context.TryGetEntity(entityInfo.unitId, out _))
        {
            return;
        }

        if (NetworkEntitySpawnUtility.TrySpawn(context.EntityManager, entityInfo, out Entity entity))
        {
            context.RegisterEntity(entityInfo.unitId, entity);
        }
    }
}
