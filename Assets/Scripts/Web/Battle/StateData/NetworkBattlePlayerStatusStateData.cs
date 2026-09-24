using System;
using Unity.Entities;

[Serializable]
public sealed class NetworkBattlePlayerStatusStateData : NetworkStateData
{
    public BattlePlayerLifeState lifeState;
    public BattlePlayerConnectionState connectionState;
    public byte transitionReady;

    public override void Apply(NetworkStateApplyContext context)
    {
        if (!context.TryGetEntity(unitId, out Entity entity))
            return;

        BattlePlayerStatusUtility.Apply(context.EntityManager, entity, new BattlePlayerStatusComponent
        {
            LifeState = lifeState,
            ConnectionState = connectionState,
            TransitionReady = transitionReady,
            NetworkDirty = 0,
        });
    }
}
