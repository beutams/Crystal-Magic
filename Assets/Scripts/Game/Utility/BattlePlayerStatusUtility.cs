using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;

public static class BattlePlayerStatusUtility
{
    public static bool IsSpectator(EntityManager entityManager, Entity entity)
    {
        return entityManager.HasComponent<BattlePlayerStatusComponent>(entity) &&
               entityManager.GetComponentData<BattlePlayerStatusComponent>(entity).IsSpectator;
    }

    public static bool IsInputLocked(EntityManager entityManager, Entity entity)
    {
        return entityManager.HasComponent<BattlePlayerStatusComponent>(entity) &&
               entityManager.GetComponentData<BattlePlayerStatusComponent>(entity).IsInputLocked;
    }

    public static void Apply(EntityManager entityManager, Entity entity, BattlePlayerStatusComponent status)
    {
        if (entityManager.HasComponent<BattlePlayerStatusComponent>(entity))
            entityManager.SetComponentData(entity, status);
        else
            entityManager.AddComponentData(entity, status);

        if (status.IsSpectator)
        {
            if (entityManager.HasBuffer<PlayerInputEventElement>(entity))
                entityManager.GetBuffer<PlayerInputEventElement>(entity).Clear();
            if (!entityManager.HasComponent<BattleSpectatorComponent>(entity))
            {
                BattleSpectatorComponent spectator = new();
                if (entityManager.HasComponent<PhysicsCollider>(entity))
                {
                    spectator.Collider = entityManager.GetComponentData<PhysicsCollider>(entity);
                    spectator.HadCollider = 1;
                    entityManager.RemoveComponent<PhysicsCollider>(entity);
                }
                entityManager.AddComponentData(entity, spectator);
            }

            if (entityManager.HasComponent<PlayerInputComponent>(entity))
            {
                PlayerInputComponent oldInput = entityManager.GetComponentData<PlayerInputComponent>(entity);
                entityManager.SetComponentData(entity, new PlayerInputComponent
                {
                    Move = status.ConnectionState == BattlePlayerConnectionState.Online ? oldInput.Move : float2.zero,
                    PointerWorldPosition = oldInput.PointerWorldPosition,
                    SkillChainIndex = oldInput.SkillChainIndex,
                    NetworkDirty = 1,
                });
            }
            if (entityManager.HasComponent<PhysicsVelocity>(entity))
                entityManager.SetComponentData(entity, new PhysicsVelocity());
        }
        else if (entityManager.HasComponent<BattleSpectatorComponent>(entity))
        {
            BattleSpectatorComponent spectator = entityManager.GetComponentData<BattleSpectatorComponent>(entity);
            if (spectator.HadCollider != 0)
                entityManager.AddComponentData(entity, spectator.Collider);
            entityManager.RemoveComponent<BattleSpectatorComponent>(entity);
            // 被暂停的施法/等待节点不能在重连后继续停在旧动作中。
            if (entityManager.HasBuffer<StateScriptGraphStateElement>(entity))
            {
                DynamicBuffer<StateScriptGraphStateElement> graphs = entityManager.GetBuffer<StateScriptGraphStateElement>(entity);
                for (int index = 0; index < graphs.Length; index++)
                    graphs[index] = new StateScriptGraphStateElement { NodeStateStart = graphs[index].NodeStateStart };
                DynamicBuffer<StateScriptNodeStateElement> nodes = entityManager.GetBuffer<StateScriptNodeStateElement>(entity);
                for (int index = 0; index < nodes.Length; index++)
                    nodes[index] = default;
                entityManager.GetBuffer<StateScriptExternalResultElement>(entity).Clear();
                entityManager.GetBuffer<StateScriptManagedCommandElement>(entity).Clear();
                entityManager.GetBuffer<StateScriptSourceCommandElement>(entity).Clear();
                entityManager.GetBuffer<StateScriptSourceCommandArgumentElement>(entity).Clear();
            }
        }

        if (status.IsWaitingForTransition)
        {
            if (entityManager.HasBuffer<PlayerInputEventElement>(entity))
                entityManager.GetBuffer<PlayerInputEventElement>(entity).Clear();
            if (entityManager.HasComponent<PlayerInputComponent>(entity))
            {
                PlayerInputComponent input = entityManager.GetComponentData<PlayerInputComponent>(entity);
                input.Move = float2.zero;
                input.IsPrimaryHeld = 0;
                input.ContinuousPrimaryHeld = 0;
                input.IsInteractHeld = 0;
                input.IsInventoryHeld = 0;
                input.IsPropertyHeld = 0;
                input.IsEscapeHeld = 0;
                input.IsSkillHeld = 0;
                input.IsUsePropHeld = 0;
                input.PropIndex = -1;
                input.NetworkDirty = 1;
                entityManager.SetComponentData(entity, input);
            }
            if (entityManager.HasComponent<PhysicsVelocity>(entity))
                entityManager.SetComponentData(entity, new PhysicsVelocity());
        }
    }
}
