using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 状态机工厂
/// 持有三类对象的创建函数，由 StateMachineRegistry 在初始化时注册，
/// 由 UnitStateMachineSystem 在构建状态机图时调用。
/// 与 System 和 Registry 均解耦，可独立测试。
/// </summary>
public class StateMachineFactory
{
    private readonly Dictionary<string, Func<AUnitState>>          _stateFactories   = new();
    private readonly Dictionary<string, Func<ISource>>             _sourceFactories  = new();
    private readonly Dictionary<string, Func<float, ICompareType>> _compareFactories = new();

    // ════════════════════════════════════════════════
    //  注册接口（供 StateMachineRegistry 调用）
    // ════════════════════════════════════════════════

    /// <summary>注册 AUnitState 子类，要求有无参构造。</summary>
    public void RegisterState<T>() where T : AUnitState, new()
        => _stateFactories[typeof(T).Name] = static () => new T();

    /// <summary>注册 ISource 实现，要求有无参构造。</summary>
    public void RegisterSource<T>() where T : ISource, new()
        => _sourceFactories[typeof(T).Name] = static () => new T();

    /// <summary>
    /// 注册 ICompareType 实现。
    /// 带 value 字段传 <c>v => new T { value = v }</c>；
    /// 无 value 字段传 <c>_ => new T()</c>。
    /// </summary>
    public void RegisterCompareType<T>(Func<float, T> factory) where T : ICompareType
        => _compareFactories[typeof(T).Name] = v => factory(v);

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

    public ISource CreateSource(string typeName)
    {
        if (!_sourceFactories.TryGetValue(typeName, out var factory))
        {
            Debug.LogError($"[StateMachineFactory] 未注册 ISource: {typeName}，请重新生成 StateMachineRegistry");
            return null;
        }
        return factory();
    }

    public ICompareType CreateCompareType(string typeName, float value)
    {
        if (!_compareFactories.TryGetValue(typeName, out var factory))
        {
            Debug.LogError($"[StateMachineFactory] 未注册 ICompareType: {typeName}，请重新生成 StateMachineRegistry");
            return null;
        }
        return factory(value);
    }

    // ════════════════════════════════════════════════
    //  统计（调试用）
    // ════════════════════════════════════════════════

    public int StateCount   => _stateFactories.Count;
    public int SourceCount  => _sourceFactories.Count;
    public int CompareCount => _compareFactories.Count;
}
