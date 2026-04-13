using System.Collections.Generic;

/// <summary>
/// 通用条件配置（可序列化，供 Skill / Effect / Unit 状态机等共用）
/// 运行时由 ComparatorFactory.BuildComparator 构建为 Comparator 实例
/// </summary>
[System.Serializable]
public class ConditionConfig
{
    /// <summary>Necessary = 必须满足；Unallowed = 不能满足</summary>
    public ConditionType ConditionType = ConditionType.Necessary;
    /// <summary>ISource 实现类名称</summary>
    public string SourceType = "";
    /// <summary>ICompareType 实现类名称（GreaterThan / LessThan / Equal / IsTrue / IsFalse）</summary>
    public string CompareType = "";
    /// <summary>比较阈值（GreaterThan / LessThan / Equal 有效）</summary>
    public float CompareValue;
}
