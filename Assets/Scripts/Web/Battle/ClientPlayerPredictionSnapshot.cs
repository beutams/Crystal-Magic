using System;
using System.Collections.Generic;
using Unity.Entities;

/// <summary>
/// 本地玩家在一个已完成逻辑帧末尾的预测快照。
/// NetworkStates 继续复用现有 NetworkStateData 比对；SS 运行态只保存在本地，不参与网络传输。
/// </summary>
public sealed class ClientPlayerPredictionSnapshot
{
    public Queue<NetworkStateData> NetworkStates { get; private set; } = new();
    public UnitStateScriptComponent StateScript;
    public StateScriptGraphStateElement[] GraphStates = Array.Empty<StateScriptGraphStateElement>();
    public StateScriptNodeStateElement[] NodeStates = Array.Empty<StateScriptNodeStateElement>();
    public bool HasStateScript;

    public static ClientPlayerPredictionSnapshot Capture(
        EntityManager entityManager,
        Entity entity,
        Guid unitId)
    {
        ClientPlayerPredictionSnapshot snapshot = new()
        {
            NetworkStates = NetworkUnitStateSnapshotUtility.CapturePlayerState(
                entityManager,
                entity,
                unitId),
        };

        if (!entityManager.HasComponent<UnitStateScriptComponent>(entity) ||
            !entityManager.HasBuffer<StateScriptGraphStateElement>(entity) ||
            !entityManager.HasBuffer<StateScriptNodeStateElement>(entity))
        {
            return snapshot;
        }

        snapshot.HasStateScript = true;
        snapshot.StateScript = entityManager.GetComponentData<UnitStateScriptComponent>(entity);
        DynamicBuffer<StateScriptGraphStateElement> graphs =
            entityManager.GetBuffer<StateScriptGraphStateElement>(entity, true);
        DynamicBuffer<StateScriptNodeStateElement> nodes =
            entityManager.GetBuffer<StateScriptNodeStateElement>(entity, true);
        snapshot.GraphStates = new StateScriptGraphStateElement[graphs.Length];
        snapshot.NodeStates = new StateScriptNodeStateElement[nodes.Length];
        for (int index = 0; index < graphs.Length; index++)
            snapshot.GraphStates[index] = graphs[index];
        for (int index = 0; index < nodes.Length; index++)
            snapshot.NodeStates[index] = nodes[index];
        return snapshot;
    }

    public bool RestoreStateScript(EntityManager entityManager, Entity entity)
    {
        if (!HasStateScript ||
            !entityManager.HasComponent<UnitStateScriptComponent>(entity) ||
            !entityManager.HasBuffer<StateScriptGraphStateElement>(entity) ||
            !entityManager.HasBuffer<StateScriptNodeStateElement>(entity))
        {
            return false;
        }

        DynamicBuffer<StateScriptGraphStateElement> graphs =
            entityManager.GetBuffer<StateScriptGraphStateElement>(entity);
        DynamicBuffer<StateScriptNodeStateElement> nodes =
            entityManager.GetBuffer<StateScriptNodeStateElement>(entity);
        if (graphs.Length != GraphStates.Length || nodes.Length != NodeStates.Length)
            return false;

        entityManager.SetComponentData(entity, StateScript);
        for (int index = 0; index < graphs.Length; index++)
            graphs[index] = GraphStates[index];
        for (int index = 0; index < nodes.Length; index++)
            nodes[index] = NodeStates[index];
        return true;
    }

    public bool TryGetMoveState(out NetworkMoveStateData moveState)
    {
        foreach (NetworkStateData state in NetworkStates)
        {
            if (state is NetworkMoveStateData move)
            {
                moveState = move;
                return true;
            }
        }

        moveState = null;
        return false;
    }
}
