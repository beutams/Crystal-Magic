using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(UnitInitializationSystemGroup))]
partial class UnitStateMachineBuildSystem : SystemBase
{
    private StateMachineFactory _factory;
    private ComparatorFactory _comparatorFactory;

    protected override void OnCreate()
    {
        base.OnCreate();
        _factory = new StateMachineFactory();
        _comparatorFactory = new ComparatorFactory();
        StateMachineRegistry.RegisterAll(_factory);
        ComparatorRegistry.RegisterAll(_comparatorFactory);
        Debug.Log($"[StateMachine] 工厂注册完成 - State: {_factory.StateCount}  ISource: {_comparatorFactory.SourceCount}  ICompareType: {_comparatorFactory.CompareCount}");
    }

    protected override void OnUpdate()
    {
        foreach (var (sm, entity) in SystemAPI.Query<UnitStateMachineComponent>().WithEntityAccess())
        {
            if (sm.CurrentState != null)
                continue;

            TryBuild(sm, entity);
        }
    }

    private void TryBuild(UnitStateMachineComponent sm, Entity entity)
    {
        if (sm.UnitDataId < 0 && string.IsNullOrEmpty(sm.UnitName))
        {
            Debug.LogWarning($"[StateMachine] Entity {entity} 的 UnitDataId 和 UnitName 都为空，跳过初始化");
            return;
        }

        UnitData data = sm.UnitDataId >= 0 ? DataComponent.Instance?.Get<UnitData>(sm.UnitDataId) : DataComponent.Instance?.Find<UnitData>(row => row.Name == sm.UnitName);
        if (data == null)
        {
            Debug.LogError($"[StateMachine] 找不到 UnitData: Id={sm.UnitDataId}, Name={sm.UnitName}");
            return;
        }

        UnitStateMachineModuleData stateModule = data.GetModule<UnitStateMachineModuleData>();
        List<UnitStateConfig> states = stateModule?.States;
        if (states == null || states.Count == 0)
        {
            Debug.LogWarning($"[StateMachine] {sm.UnitName} 没有配置任何状态");
            return;
        }

        var stateMap = new Dictionary<string, AUnitState>(states.Count);
        foreach (UnitStateConfig config in states)
        {
            AUnitState state = _factory.CreateState(config.StateType);
            if (state != null)
                stateMap[config.StateType] = state;
        }

        foreach (AUnitState state in stateMap.Values)
            state.OnInitialize(entity, EntityManager);

        foreach (UnitStateConfig config in states)
        {
            if (!stateMap.TryGetValue(config.StateType, out AUnitState sourceState))
                continue;

            sourceState.transitions = new Dictionary<AUnitState, Comparator>(config.Transitions.Count);
            foreach (UnitTransitionConfig transitionConfig in config.Transitions)
            {
                if (!stateMap.TryGetValue(transitionConfig.TargetStateType, out AUnitState targetState))
                {
                    Debug.LogWarning($"[StateMachine] [{sm.UnitName}] 找不到目标状态 {transitionConfig.TargetStateType}");
                    continue;
                }

                sourceState.transitions[targetState] =
                    _comparatorFactory.BuildComparator(transitionConfig.Conditions, entity, EntityManager);
            }
        }

        if (!stateMap.TryGetValue(states[0].StateType, out AUnitState initialState))
        {
            Debug.LogError($"[StateMachine] [{sm.UnitName}] 初始状态实例缺失");
            return;
        }

        sm.StateInstances = stateMap;
        sm.InitialState = initialState;
        sm.InitialStateName = initialState.GetType().Name;
        sm.CurrentState = null;
        sm.PreviousState = null;
        sm.StateTime = 0f;
        sm.CurrentStateName = "None";
        sm.PreviousStateName = "None";

        Debug.Log($"[StateMachine] [{sm.UnitName}] 构建完成，初始 {states[0].StateType}，共 {stateMap.Count} 个状态");
    }
}
