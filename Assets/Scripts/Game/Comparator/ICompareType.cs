using System;

public interface ICompareType
{
    public bool Compare(ISource obj);
}
[FactoryKey("GreaterThan")]
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
public struct IsTrue : ICompareType
{
    public bool Compare(ISource obj)
    {
        return obj.GetValue() > 0;
    }
}
[FactoryKey("IsFalse")]
public struct IsFalse : ICompareType
{
    public bool Compare(ISource obj)
    {
        return obj.GetValue() <= 0;
    }
}
