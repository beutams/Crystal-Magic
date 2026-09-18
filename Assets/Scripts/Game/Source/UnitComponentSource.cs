using System;

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class UnitSourceProviderAttribute : Attribute
{
    public UnitSourceProviderAttribute(Type componentType, Type authoringType = null, bool isGlobal = false)
    {
        ComponentType = componentType;
        AuthoringType = authoringType;
        IsGlobal = isGlobal;
    }

    public Type ComponentType { get; }
    public Type AuthoringType { get; }
    public bool IsGlobal { get; }
}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class UnitSourceGetAttribute : Attribute
{
    public UnitSourceGetAttribute(
        int operation,
        string key,
        UnitValueCategory returnType,
        params UnitValueCategory[] parameterTypes)
    {
        Operation = operation;
        Key = key;
        ReturnType = returnType;
        ParameterTypes = parameterTypes ?? Array.Empty<UnitValueCategory>();
    }

    public int Operation { get; }
    public string Key { get; }
    public UnitValueCategory ReturnType { get; }
    public UnitValueCategory[] ParameterTypes { get; }
    public string[] ParameterNames { get; set; } = Array.Empty<string>();
}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = true)]
public sealed class UnitSourceSetAttribute : Attribute
{
    public UnitSourceSetAttribute(int operation, string key, params UnitValueCategory[] parameterTypes)
    {
        Operation = operation;
        Key = key;
        ParameterTypes = parameterTypes ?? Array.Empty<UnitValueCategory>();
    }

    public int Operation { get; }
    public string Key { get; }
    public UnitValueCategory[] ParameterTypes { get; }
    public string[] ParameterNames { get; set; } = Array.Empty<string>();
    public bool RequiresKey { get; set; }
}
