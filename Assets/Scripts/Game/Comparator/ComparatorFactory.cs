using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class ComparatorFactory
{
    private readonly GeneratedFactory<string, ISource> _sourceFactories = new(StringComparer.Ordinal);
    private readonly GeneratedFactory<string, float, ICompareType> _compareFactories = new(StringComparer.Ordinal);
    public void RegisterSource(string key, Func<ISource> factory)
    {
        _sourceFactories.Register(key, factory);
    }
    public void RegisterCompareType(string key, Func<float, ICompareType> factory)
    {
        _compareFactories.Register(key, factory);
    }

    public ISource CreateSource(string typeName)
    {
        ISource source = _sourceFactories.Create(typeName);
        if (source == null)
        {
            Debug.LogError($"[ComparatorFactory] 未注册 ISource: {typeName}");
            return null;
        }
        return source;
    }

    public ICompareType CreateCompareType(string typeName, float value)
    {
        ICompareType compareType = _compareFactories.Create(typeName, value);
        if (compareType == null)
        {
            Debug.LogError($"[ComparatorFactory] 未注册 ICompareType: {typeName}");
            return null;
        }
        return compareType;
    }
    public Comparator BuildComparator(List<ConditionConfig> configs, Entity entity, EntityManager em, Entity originEntity = default, bool hasOriginEntity = false)
    {
        if (configs == null || configs.Count == 0)
            return new Comparator { conditions = Array.Empty<Condition>() };

        SourceContext context = new(entity, em, originEntity, hasOriginEntity);
        var conditions = new List<Condition>(configs.Count);
        foreach (var cfg in configs)
        {
            var cond = BuildCondition(cfg, context);
            if (cond != null) conditions.Add(cond);
        }
        return new Comparator { conditions = conditions.ToArray() };
    }

    private Condition BuildCondition(ConditionConfig cfg, in SourceContext context)
    {
        ISource source = CreateSource(cfg.SourceType);
        if (source == null) return null;

        source.Init(context);

        ICompareType compareType = CreateCompareType(cfg.CompareType, cfg.CompareValue);
        if (compareType == null) return null;

        return new RuntimeCondition
        {
            source      = source,
            compareType = compareType,
            type        = cfg.ConditionType,
        };
    }
    public int SourceCount  => _sourceFactories.Count;
    public int CompareCount => _compareFactories.Count;
}
