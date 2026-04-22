using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class FactoryKeyAttribute : Attribute
{
    public FactoryKeyAttribute(string key, int order = 0, string displayName = null)
    {
        Key = key;
        Order = order;
        DisplayName = displayName;
    }

    public string Key { get; }
    public int Order { get; }
    public string DisplayName { get; }
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class FactoryInputMemberAttribute : Attribute
{
    public FactoryInputMemberAttribute(string memberName)
    {
        MemberName = memberName;
    }

    public string MemberName { get; }
}

public readonly struct FactoryTypeInfo
{
    public FactoryTypeInfo(string key, string displayName, Type type, int order)
    {
        Key = key;
        DisplayName = displayName;
        Type = type;
        Order = order;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public Type Type { get; }
    public int Order { get; }
}
