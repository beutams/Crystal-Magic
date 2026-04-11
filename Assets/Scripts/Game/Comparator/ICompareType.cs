using System;

public interface ICompareType
{
    public bool Compare(ISource obj);
}
public struct GreaterThan : ICompareType
{
    public float value;

    public bool Compare(ISource obj)
    {
        return obj.GetValue() > value;
    }
}
public struct LessThan : ICompareType
{
    public float value;

    public bool Compare(ISource obj)
    {
        return obj.GetValue() < value;
    }
}
public struct Equal : ICompareType
{
    public float value;

    public bool Compare(ISource obj)
    {
        return MathF.Abs(obj.GetValue() - value) < 0.0001f;
    }
}
public struct IsTrue : ICompareType
{
    public bool Compare(ISource obj)
    {
        return obj.GetValue() > 0;
    }
}
public struct IsFalse : ICompareType
{
    public bool Compare(ISource obj)
    {
        return obj.GetValue() <= 0;
    }
}