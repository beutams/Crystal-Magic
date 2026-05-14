using System;

public interface ICompareType
{
    public bool Compare(ISource obj);
}
[FactoryKey("GreaterThan")]
[EditorLabel("大于")]
[FactoryInputMember("value")]
public struct GreaterThan : ICompareType
{
    public float value;

    public bool Compare(ISource obj)
    {
        return obj.GetValue() > value;
    }
}
[FactoryKey("LessThan")]
[EditorLabel("小于")]
[FactoryInputMember("value")]
public struct LessThan : ICompareType
{
    public float value;

    public bool Compare(ISource obj)
    {
        return obj.GetValue() < value;
    }
}
[FactoryKey("Equal")]
[EditorLabel("等于")]
[FactoryInputMember("value")]
public struct Equal : ICompareType
{
    public float value;

    public bool Compare(ISource obj)
    {
        return MathF.Abs(obj.GetValue() - value) < 0.0001f;
    }
}
[FactoryKey("IsTrue")]
[EditorLabel("为真")]
public struct IsTrue : ICompareType
{
    public bool Compare(ISource obj)
    {
        return obj.GetValue() > 0;
    }
}
[FactoryKey("IsFalse")]
[EditorLabel("为假")]
public struct IsFalse : ICompareType
{
    public bool Compare(ISource obj)
    {
        return obj.GetValue() <= 0;
    }
}
