using System;
using System.Linq;
using System.Reflection;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class EditorLabelAttribute : Attribute
{
    public EditorLabelAttribute(string label)
    {
        Label = label ?? string.Empty;
    }

    public string Label { get; }
}

public readonly struct EditorTypeDisplayEntry
{
    public EditorTypeDisplayEntry(string key, string displayName, Type type)
    {
        Key = key ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Type = type;
    }

    public string Key { get; }

    public string DisplayName { get; }

    public Type Type { get; }
}

public static class EditorLabelUtility
{
    public static string GetEnumValueLabel(Enum value)
    {
        if (value == null)
            return string.Empty;

        Type enumType = value.GetType();
        string memberName = Enum.GetName(enumType, value);
        if (string.IsNullOrWhiteSpace(memberName))
            return value.ToString();

        FieldInfo field = enumType.GetField(memberName, BindingFlags.Public | BindingFlags.Static);
        return field == null ? memberName : GetLabel(field, memberName);
    }

    public static string[] GetEnumDisplayNames<TEnum>() where TEnum : Enum
    {
        return Enum.GetValues(typeof(TEnum))
            .Cast<Enum>()
            .Select(GetEnumValueLabel)
            .ToArray();
    }

    public static string GetLabel(FieldInfo field)
    {
        return field == null ? string.Empty : GetLabel(field, field.Name);
    }

    public static string GetLabel(MemberInfo member, string fallback)
    {
        if (member == null)
            return fallback ?? string.Empty;

        EditorLabelAttribute labelAttribute = member.GetCustomAttribute<EditorLabelAttribute>(false);
        if (labelAttribute != null && !string.IsNullOrWhiteSpace(labelAttribute.Label))
            return labelAttribute.Label;

        if (member is Type type)
        {
            FactoryKeyAttribute factoryKeyAttribute = type.GetCustomAttribute<FactoryKeyAttribute>(false);
            if (factoryKeyAttribute != null && !string.IsNullOrWhiteSpace(factoryKeyAttribute.DisplayName))
                return factoryKeyAttribute.DisplayName;
        }

        return fallback ?? member.Name;
    }

    public static string GetTypeDisplayName(string typeName, Type baseType = null, bool subclass = false)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return string.Empty;

        Type type = FindType(typeName, baseType, subclass);
        return type == null ? typeName : GetLabel(type, typeName);
    }

    public static EditorTypeDisplayEntry[] CollectTypeEntries(Type baseType, bool subclass = false)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch
                {
                    return Array.Empty<Type>();
                }
            })
            .Where(type => !type.IsAbstract &&
                           !type.IsInterface &&
                           (subclass ? type.IsSubclassOf(baseType) : baseType.IsAssignableFrom(type)))
            .OrderBy(type => type.Name)
            .Select(type => new EditorTypeDisplayEntry(type.Name, GetLabel(type, type.Name), type))
            .ToArray();
    }

    private static Type FindType(string typeName, Type baseType, bool subclass)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly =>
            {
                try
                {
                    return assembly.GetTypes();
                }
                catch
                {
                    return Array.Empty<Type>();
                }
            })
            .FirstOrDefault(type =>
                string.Equals(type.Name, typeName, StringComparison.Ordinal) &&
                (baseType == null ||
                 (subclass ? type.IsSubclassOf(baseType) : baseType.IsAssignableFrom(type))));
    }
}
