using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;
using UnityEngine;

// ══════════════════════════════════════════════════════════════════════════════
//  UnitStateMachineSystem
//  职责：
//    1. OnCreate  — 创建 StateMachineFactory，调用 StateMachineRegistry.RegisterAll
//    2. OnUpdate  — 首帧为 Entity 构建状态机图；后续帧累加时间并调用 OnUpdate
//  执行顺序：先于 UnitStateTransitionSystem（先 Update 再检测出口条件）
// ══════════════════════════════════════════════════════════════════════════════
[UpdateBefore(typeof(UnitStateTransitionSystem))]
partial class UnitStateMachineSystem : SystemBase
{
    private StateMachineFactory _factory;

    // ════════════════════════════════════════════════
    //  生命周期
    // ════════════════════════════════════════════════

    protected override void OnCreate()
    {
        base.OnCreate();
        _factory = new StateMachineFactory();
        StateMachineRegistry.RegisterAll(_factory);
        Debug.Log($"[StateMachine] 工厂注册完成 —— " + $"状态: {_factory.StateCount}  ISource: {_factory.SourceCount}  ICompareType: {_factory.CompareCount}");
    }

    protected override void OnUpdate()
    {
        float dt = SystemAPI.Time.DeltaTime;

        foreach (var (sm, entity) in
                 SystemAPI.Query<UnitStateMachineComponent>().WithEntityAccess())
        {
            if (sm.CurrentState == null)
            {
                TryBuild(sm, entity);
                continue; // 构建帧不执行 OnUpdate，等下一帧
            }

            sm.StateTime += dt;
            sm.CurrentState.OnUpdate(dt);
        }
    }

    // ════════════════════════════════════════════════
    //  构建状态机图
    // ════════════════════════════════════════════════

    private void TryBuild(UnitStateMachineComponent sm, Entity entity)
    {
        if (string.IsNullOrEmpty(sm.UnitName))
        {
            Debug.LogWarning($"[StateMachine] Entity {entity} 的 UnitName 为空，跳过初始化");
            return;
        }

        UnitData data = DataComponent.Instance?.Find<UnitData>(r => r.Name == sm.UnitName);
        if (data == null)
        {
            Debug.LogError($"[StateMachine] 找不到 UnitData: {sm.UnitName}，请检查 UnitDataTable.json");
            return;
        }

        if (data.States == null || data.States.Count == 0)
        {
            Debug.LogWarning($"[StateMachine] {sm.UnitName} 没有配置任何状态");
            return;
        }

        // Step 1：实例化所有状态
        var stateMap = new Dictionary<string, AUnitState>(data.States.Count);
        foreach (var cfg in data.States)
        {
            AUnitState state = _factory.CreateState(cfg.StateType);
            if (state != null) stateMap[cfg.StateType] = state;
        }

        if (stateMap.Count == 0)
        {
            Debug.LogError($"[StateMachine] {sm.UnitName} 无法实例化任何状态，" +
                           "请检查类名或重新生成 StateMachineRegistry");
            return;
        }

        // Step 2：注入 Entity / EntityManager
        foreach (var state in stateMap.Values)
            state.OnInitialize(entity, EntityManager);

        // Step 3：组装 transitions 字典
            foreach (var cfg in data.States)
            {
                if (!stateMap.TryGetValue(cfg.StateType, out var src)) continue;
                src.transitions = new Dictionary<AUnitState, Comparator>(cfg.Transitions.Count);

                foreach (var transCfg in cfg.Transitions)
                {
                    if (!stateMap.TryGetValue(transCfg.TargetStateType, out var dst))
                    {
                        Debug.LogWarning($"[StateMachine] [{sm.UnitName}] 找不到目标状态: {transCfg.TargetStateType}");
                        continue;
                    }
                    src.transitions[dst] = BuildComparator(transCfg, entity);
                }
        }

        // Step 4：进入初始状态
        if (!stateMap.TryGetValue(data.States[0].StateType, out var initial))
        {
            Debug.LogError($"[StateMachine] [{sm.UnitName}] 初始状态实例缺失");
            return;
        }

        sm.StateInstances    = stateMap;
        sm.CurrentState      = initial;
        sm.PreviousState     = null;
        sm.StateTime         = 0f;
        sm.CurrentStateName  = initial.GetType().Name;
        sm.PreviousStateName = "None";
        sm.CurrentState.OnEnter();

        Debug.Log($"[StateMachine] [{sm.UnitName}] 构建完成，" +
                  $"初始: {data.States[0].StateType}，共 {stateMap.Count} 个状态");
    }

    // ════════════════════════════════════════════════
    //  Comparator / Condition 组装（委托给 Factory 创建具体对象）
    // ════════════════════════════════════════════════

    private Comparator BuildComparator(UnitTransitionConfig transCfg, Entity entity)
    {
        var conditions = new List<Condition>(transCfg.Conditions.Count);
        foreach (var condCfg in transCfg.Conditions)
        {
            var cond = BuildCondition(condCfg, entity);
            if (cond != null) conditions.Add(cond);
        }
        return new Comparator { conditions = conditions.ToArray() };
    }

    private Condition BuildCondition(UnitConditionConfig cfg, Entity entity)
    {
        ISource source = _factory.CreateSource(cfg.SourceType);
        if (source == null)
        {
            Debug.LogWarning($"[StateMachine] 未注册 ISource: {cfg.SourceType}，跳过该条件");
            return null;
        }

        source.Init(entity, EntityManager);

        ICompareType compareType = _factory.CreateCompareType(cfg.CompareType, cfg.CompareValue);
        if (compareType == null)
        {
            Debug.LogWarning($"[StateMachine] 未注册 ICompareType: {cfg.CompareType}，跳过该条件");
            return null;
        }

        return new RuntimeCondition
        {
            source      = source,
            compareType = compareType,
            type        = cfg.ConditionType,
        };
    }
}
