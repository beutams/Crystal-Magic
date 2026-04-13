using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// ISource / ICompareType 的注册中心 + Comparator 运行时构建器。
/// 与具体系统（状态机、技能等）解耦，任何需要条件判定的模块均可使用。
/// </summary>
public class ComparatorFactory
{
    private readonly Dictionary<string, Func<ISource>>             _sourceFactories  = new();
    private readonly Dictionary<string, Func<float, ICompareType>> _compareFactories = new();

    // ════════════════════════════════════════════════
    //  注册接口
    // ════════════════════════════════════════════════

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
    //  创建接口
    // ════════════════════════════════════════════════

    public ISource CreateSource(string typeName)
    {
        if (!_sourceFactories.TryGetValue(typeName, out var factory))
        {
            Debug.LogError($"[ComparatorFactory] 未注册 ISource: {typeName}");
            return null;
        }
        return factory();
    }

    public ICompareType CreateCompareType(string typeName, float value)
    {
        if (!_compareFactories.TryGetValue(typeName, out var factory))
        {
            Debug.LogError($"[ComparatorFactory] 未注册 ICompareType: {typeName}");
            return null;
        }
        return factory(value);
    }

    // ════════════════════════════════════════════════
    //  Comparator 构建
    // ════════════════════════════════════════════════

    /// <summary>
    /// 根据条件配置列表构建运行时 Comparator。
    /// 每个 ConditionConfig 创建 ISource + ICompareType 并注入 Entity/EM。
    /// </summary>
    public Comparator BuildComparator(List<ConditionConfig> configs, Entity entity, EntityManager em)
    {
        if (configs == null || configs.Count == 0)
            return new Comparator { conditions = Array.Empty<Condition>() };

        var conditions = new List<Condition>(configs.Count);
        foreach (var cfg in configs)
        {
            var cond = BuildCondition(cfg, entity, em);
            if (cond != null) conditions.Add(cond);
        }
        return new Comparator { conditions = conditions.ToArray() };
    }

    private Condition BuildCondition(ConditionConfig cfg, Entity entity, EntityManager em)
    {
        ISource source = CreateSource(cfg.SourceType);
        if (source == null) return null;

        source.Init(entity, em);

        ICompareType compareType = CreateCompareType(cfg.CompareType, cfg.CompareValue);
        if (compareType == null) return null;

        return new RuntimeCondition
        {
            source      = source,
            compareType = compareType,
            type        = cfg.ConditionType,
        };
    }

    // ════════════════════════════════════════════════
    //  统计（调试用）
    // ════════════════════════════════════════════════

    public int SourceCount  => _sourceFactories.Count;
    public int CompareCount => _compareFactories.Count;
}
