using System;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// 状态转换系统——每帧遍历所有已初始化的状态机，
/// 按 transitions 字典顺序检查条件，第一个满足的转换立即生效。
/// 运行在 UnitStateMachineSystem 之后，保证本帧 OnUpdate 先执行再检测出口。
/// </summary>
[UpdateAfter(typeof(UnitStateMachineSystem))]
partial class UnitStateTransitionSystem : SystemBase
{
    protected override void OnUpdate()
    {
        foreach (var smComp in SystemAPI.Query<UnitStateMachineComponent>())
        {
            // 未初始化则跳过（由 UnitStateMachineSystem 负责初始化）
            if (smComp.CurrentState == null) continue;

            // 无转换规则则跳过
            var transitions = smComp.CurrentState.transitions;
            if (transitions == null || transitions.Count == 0) continue;

            // 遍历所有可能的目标状态，找到第一个满足条件的转换
            foreach (var kvp in transitions)
            {
                Comparator comparator = kvp.Value;
                AUnitState target     = kvp.Key;

                // Comparator.conditions 为空视为无条件转换（始终触发）
                if (comparator.conditions == null || comparator.GetResult())
                {
                    DoTransition(smComp, target);
                    break; // 每帧只执行一次转换
                }
            }
        }
    }

    // ────────────────────────────────────────────────
    private static void DoTransition(UnitStateMachineComponent sm, AUnitState next)
    {
        sm.CurrentState.OnExit();
        sm.PreviousState     = sm.CurrentState;
        sm.PreviousStateName = sm.CurrentStateName;
        sm.CurrentState      = next;
        sm.CurrentStateName  = next.GetType().Name;
        sm.StateTime         = 0f;
        sm.CurrentState.OnEnter();
    }
}
