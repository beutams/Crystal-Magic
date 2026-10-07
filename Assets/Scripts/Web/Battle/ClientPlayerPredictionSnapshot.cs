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
    public UnitVariableElement[] Variables = Array.Empty<UnitVariableElement>();
    public StateScriptExternalResultElement[] ExternalResults = Array.Empty<StateScriptExternalResultElement>();
    public PlayerCurrentSkillComponent CurrentSkill;
    public UnitManaComponent Mana;
    public bool HasVariables;
    public bool HasExternalResults;
    public bool HasCurrentSkill;
    public bool HasMana;
    public UnitControlRuntimeComponent Control;
    public bool HasControl;

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

        snapshot.HasControl = entityManager.HasComponent<UnitControlRuntimeComponent>(entity);
        if (snapshot.HasControl)
            snapshot.Control = entityManager.GetComponentData<UnitControlRuntimeComponent>(entity);

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

        snapshot.HasVariables = entityManager.HasBuffer<UnitVariableElement>(entity);
        if (snapshot.HasVariables)
        {
            DynamicBuffer<UnitVariableElement> variables = entityManager.GetBuffer<UnitVariableElement>(entity, true);
            snapshot.Variables = new UnitVariableElement[variables.Length];
            for (int index = 0; index < variables.Length; index++)
                snapshot.Variables[index] = variables[index];
        }
        snapshot.HasCurrentSkill = entityManager.HasComponent<PlayerCurrentSkillComponent>(entity);
        snapshot.HasExternalResults = entityManager.HasBuffer<StateScriptExternalResultElement>(entity);
        if (snapshot.HasExternalResults)
        {
            var results = entityManager.GetBuffer<StateScriptExternalResultElement>(entity, true);
            snapshot.ExternalResults = new StateScriptExternalResultElement[results.Length];
            for (int index = 0; index < results.Length; index++)
                snapshot.ExternalResults[index] = results[index];
        }
        if (snapshot.HasCurrentSkill)
            snapshot.CurrentSkill = entityManager.GetComponentData<PlayerCurrentSkillComponent>(entity);
        snapshot.HasMana = entityManager.HasComponent<UnitManaComponent>(entity);
        if (snapshot.HasMana)
            snapshot.Mana = entityManager.GetComponentData<UnitManaComponent>(entity);
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

        // 咏唱状态写在变量、当前技能和蓝量里，必须和图节点恢复到同一帧。
        if (HasVariables && entityManager.HasBuffer<UnitVariableElement>(entity))
        {
            DynamicBuffer<UnitVariableElement> variables = entityManager.GetBuffer<UnitVariableElement>(entity);
            variables.ResizeUninitialized(Variables.Length);
            for (int index = 0; index < variables.Length; index++)
                variables[index] = Variables[index];
        }
        if (HasCurrentSkill && entityManager.HasComponent<PlayerCurrentSkillComponent>(entity))
            entityManager.SetComponentData(entity, CurrentSkill);
        if (HasExternalResults && entityManager.HasBuffer<StateScriptExternalResultElement>(entity))
        {
            var results = entityManager.GetBuffer<StateScriptExternalResultElement>(entity);
            results.ResizeUninitialized(ExternalResults.Length);
            for (int index = 0; index < results.Length; index++)
                results[index] = ExternalResults[index];
        }
        if (HasMana && entityManager.HasComponent<UnitManaComponent>(entity))
            entityManager.SetComponentData(entity, Mana);
        return true;
    }

    public void RestoreControl(EntityManager entityManager, Entity entity)
    {
        if (HasControl)
        {
            if (entityManager.HasComponent<UnitControlRuntimeComponent>(entity))
                entityManager.SetComponentData(entity, Control);
            else
                entityManager.AddComponentData(entity, Control);
        }
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
