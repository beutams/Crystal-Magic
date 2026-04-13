using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 状态机工厂
/// 仅持有 AUnitState 的创建函数，由 StateMachineRegistry 在初始化时注册，
/// 由 UnitStateMachineSystem 在构建状态机图时调用。
/// ISource / ICompareType 的注册与创建已迁移至 ComparatorFactory。
/// </summary>
public class StateMachineFactory
{
    private readonly Dictionary<string, Func<AUnitState>> _stateFactories = new();

    // ════════════════════════════════════════════════
    //  注册接口（供 StateMachineRegistry 调用）
    // ════════════════════════════════════════════════

    /// <summary>注册 AUnitState 子类，要求有无参构造。</summary>
    public void RegisterState<T>() where T : AUnitState, new()
        => _stateFactories[typeof(T).Name] = static () => new T();

    // ════════════════════════════════════════════════
    //  创建接口（供 UnitStateMachineSystem 调用）
    // ════════════════════════════════════════════════

    public AUnitState CreateState(string typeName)
    {
        if (!_stateFactories.TryGetValue(typeName, out var factory))
        {
            Debug.LogError($"[StateMachineFactory] 未注册状态: {typeName}，请重新生成 StateMachineRegistry");
            return null;
        }
        return factory();
    }

    // ════════════════════════════════════════════════
    //  统计（调试用）
    // ════════════════════════════════════════════════

    public int StateCount => _stateFactories.Count;
}
