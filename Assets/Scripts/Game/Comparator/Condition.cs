public abstract class Condition
{
    public ISource       source;
    public ICompareType  compareType;
    public ConditionType type;
    public bool Compare() => compareType.Compare(source);
}

/// <summary>
/// 运行时通过反射构建的具体条件实例（无额外逻辑，仅让基类可实例化）
/// </summary>
public class RuntimeCondition : Condition { }

public enum ConditionType
{
    Necessary,   // 必须为 true
    Unallowed,   // 必须为 false
}
