public abstract class Condition
{
    public ISource       source;
    public ICompareType  compareType;
    public ConditionType type;
    public bool Compare() => compareType.Compare(source);
}
public class RuntimeCondition : Condition { }

public enum ConditionType
{
    Necessary,
    Unallowed,
}
