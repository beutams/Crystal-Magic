using Unity.Entities;

[DisableAutoCreation]
public partial class BattlePlayerSimulationSystemGroup : ComponentSystemGroup
{
    private Entity _scopeEntity;
    protected override void OnCreate()
    {
        base.OnCreate();
        EnableSystemSorting = false;
    }
    public void SetScope(Entity entity) => _scopeEntity = entity;

    protected override void OnUpdate()
    {
        if (SystemAPI.TryGetSingleton(out GameGateStateComponent gate) && gate.IsSimulationLocked)
            return;
        EntityManager.SetComponentData(_scopeEntity, new BattleSimulationScope { Pass = BattleSimulationPass.Players });
        try
        {
            base.OnUpdate();
        }
        finally
        {
            EntityManager.SetComponentData(_scopeEntity, new BattleSimulationScope { Pass = BattleSimulationPass.World });
        }
    }
}
