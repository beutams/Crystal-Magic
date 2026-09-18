#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

public static class UnitComponentSourceRegistryGenerator
{
    private const string OutputPath = "Assets/Scripts/Game/Source/UnitComponentSourceRegistry.cs";

    private enum AccessMode
    {
        ComponentGet,
        ComponentSet,
        LookupGet,
        ManagedGet,
        ManagedSet,
    }

    private sealed class ProviderInfo
    {
        public Type Type;
        public UnitSourceProviderAttribute Attribute;
        public readonly List<MethodInfo> Methods = new();
    }

    private sealed class EntryInfo
    {
        public ProviderInfo Provider;
        public MethodInfo Method;
        public UnitSourceGetAttribute Get;
        public UnitSourceSetAttribute Set;
        public AccessMode Mode;
        public Type LookupType;
        public string EnumName;
        public int Id;

        public bool IsGet => Get != null;
        public string Key => IsGet ? Get.Key : Set.Key;
        public int Operation => IsGet ? Get.Operation : Set.Operation;
        public UnitValueCategory[] ParameterTypes => IsGet ? Get.ParameterTypes : Set.ParameterTypes;
        public string[] ParameterNames => IsGet ? Get.ParameterNames : Set.ParameterNames;
    }

    [MenuItem("Tools/Registry/Unit Sources")]
    public static void Generate()
    {
        List<ProviderInfo> providers = CollectProviders();
        List<EntryInfo> entries = CollectEntries(providers);
        Validate(entries);
        AssignIds(entries);

        string source = BuildSource(providers, entries);
        string current = File.Exists(OutputPath) ? File.ReadAllText(OutputPath) : string.Empty;
        if (string.Equals(current, source, StringComparison.Ordinal))
            return;

        File.WriteAllText(OutputPath, source, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);
        Debug.Log($"[UnitComponentSourceRegistryGenerator] Generated {entries.Count} sources: {OutputPath}");
    }

    private static List<ProviderInfo> CollectProviders()
    {
        return TypeCache.GetTypesWithAttribute<UnitSourceProviderAttribute>()
            .Where(type => type != null)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => new ProviderInfo
            {
                Type = type,
                Attribute = type.GetCustomAttribute<UnitSourceProviderAttribute>(),
            })
            .ToList();
    }

    private static List<EntryInfo> CollectEntries(List<ProviderInfo> providers)
    {
        List<EntryInfo> entries = new();
        for (int providerIndex = 0; providerIndex < providers.Count; providerIndex++)
        {
            ProviderInfo provider = providers[providerIndex];
            MethodInfo[] methods = provider.Type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            for (int methodIndex = 0; methodIndex < methods.Length; methodIndex++)
            {
                MethodInfo method = methods[methodIndex];
                UnitSourceGetAttribute[] gets = method.GetCustomAttributes<UnitSourceGetAttribute>().ToArray();
                UnitSourceSetAttribute[] sets = method.GetCustomAttributes<UnitSourceSetAttribute>().ToArray();
                if (gets.Length == 0 && sets.Length == 0)
                    continue;

                provider.Methods.Add(method);
                Type lookupType = ResolveLookupType(method);
                AccessMode getMode = ResolveAccessMode(method, true);
                AccessMode setMode = ResolveAccessMode(method, false);
                for (int index = 0; index < gets.Length; index++)
                {
                    entries.Add(new EntryInfo
                    {
                        Provider = provider,
                        Method = method,
                        Get = gets[index],
                        Mode = getMode,
                        LookupType = lookupType,
                    });
                }

                for (int index = 0; index < sets.Length; index++)
                {
                    entries.Add(new EntryInfo
                    {
                        Provider = provider,
                        Method = method,
                        Set = sets[index],
                        Mode = setMode,
                        LookupType = lookupType,
                    });
                }
            }
        }

        return entries
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ThenBy(entry => entry.IsGet ? 0 : 1)
            .ToList();
    }

    private static AccessMode ResolveAccessMode(MethodInfo method, bool isGet)
    {
        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Any(parameter => parameter.ParameterType == typeof(EntityManager)))
            return isGet ? AccessMode.ManagedGet : AccessMode.ManagedSet;

        if (parameters.Any(parameter =>
                parameter.ParameterType.IsByRef &&
                parameter.ParameterType.GetElementType()?.IsGenericType == true &&
                parameter.ParameterType.GetElementType()?.GetGenericTypeDefinition() == typeof(ComponentLookup<>)))
        {
            return AccessMode.LookupGet;
        }

        return isGet ? AccessMode.ComponentGet : AccessMode.ComponentSet;
    }

    private static Type ResolveLookupType(MethodInfo method)
    {
        ParameterInfo parameter = method.GetParameters().FirstOrDefault(value =>
        {
            Type type = value.ParameterType.IsByRef
                ? value.ParameterType.GetElementType()
                : value.ParameterType;
            return type?.IsGenericType == true && type.GetGenericTypeDefinition() == typeof(ComponentLookup<>);
        });
        if (parameter == null)
            return null;

        Type parameterType = parameter.ParameterType.IsByRef
            ? parameter.ParameterType.GetElementType()
            : parameter.ParameterType;
        return parameterType.GetGenericArguments()[0];
    }

    private static void Validate(List<EntryInfo> entries)
    {
        HashSet<string> keys = new(StringComparer.Ordinal);
        for (int index = 0; index < entries.Count; index++)
        {
            EntryInfo entry = entries[index];
            if (string.IsNullOrWhiteSpace(entry.Key))
                throw new InvalidOperationException($"Unit source key is empty: {entry.Provider.Type.FullName}.{entry.Method.Name}");
            if (!keys.Add(entry.Key))
                throw new InvalidOperationException($"Duplicate unit source key: {entry.Key}");
            if (entry.Method.ReturnType != typeof(bool))
                throw new InvalidOperationException($"Unit source method must return bool: {entry.Provider.Type.FullName}.{entry.Method.Name}");
            if (entry.ParameterTypes.Any(type => type == UnitValueCategory.None))
                throw new InvalidOperationException($"Unit source parameters cannot use None: {entry.Key}");
            if (entry.ParameterNames.Length != 0 && entry.ParameterNames.Length != entry.ParameterTypes.Length)
                throw new InvalidOperationException($"Unit source parameter name count does not match: {entry.Key}");

            ValidateSignature(entry);
        }
    }

    private static void ValidateSignature(EntryInfo entry)
    {
        ParameterInfo[] parameters = entry.Method.GetParameters();
        bool valid = entry.Mode switch
        {
            AccessMode.ComponentGet => parameters.Length == 4 &&
                                       parameters[0].ParameterType == typeof(int) &&
                                       IsReadOnlyByRef(parameters[1], entry.Provider.Attribute.ComponentType) &&
                                       IsReadOnlyByRef(parameters[2], typeof(UnitSourceArguments)) &&
                                       IsOut(parameters[3], typeof(UnitSourceValue)),
            AccessMode.ComponentSet => parameters.Length == 3 &&
                                       parameters[0].ParameterType == typeof(int) &&
                                       IsWritableByRef(parameters[1], entry.Provider.Attribute.ComponentType) &&
                                       IsReadOnlyByRef(parameters[2], typeof(UnitSourceArguments)),
            AccessMode.LookupGet => parameters.Length == 5 &&
                                    parameters[0].ParameterType == typeof(int) &&
                                    parameters[1].ParameterType == typeof(Entity) &&
                                    IsReadOnlyByRef(parameters[2], typeof(ComponentLookup<>).MakeGenericType(entry.LookupType)) &&
                                    IsReadOnlyByRef(parameters[3], typeof(UnitSourceArguments)) &&
                                    IsOut(parameters[4], typeof(UnitSourceValue)),
            AccessMode.ManagedGet => parameters.Length == 5 &&
                                     parameters[0].ParameterType == typeof(int) &&
                                     parameters[1].ParameterType == typeof(EntityManager) &&
                                     parameters[2].ParameterType == typeof(Entity) &&
                                     IsReadOnlyByRef(parameters[3], typeof(UnitSourceArguments)) &&
                                     IsOut(parameters[4], typeof(UnitSourceValue)),
            AccessMode.ManagedSet => parameters.Length == 4 &&
                                     parameters[0].ParameterType == typeof(int) &&
                                     parameters[1].ParameterType == typeof(EntityManager) &&
                                     parameters[2].ParameterType == typeof(Entity) &&
                                     IsReadOnlyByRef(parameters[3], typeof(UnitSourceArguments)),
            _ => false,
        };

        if (!valid)
            throw new InvalidOperationException($"Unsupported unit source signature: {entry.Provider.Type.FullName}.{entry.Method.Name}");
    }

    private static bool IsReadOnlyByRef(ParameterInfo parameter, Type type)
    {
        return parameter.ParameterType.IsByRef && parameter.IsIn &&
               parameter.ParameterType.GetElementType() == type;
    }

    private static bool IsWritableByRef(ParameterInfo parameter, Type type)
    {
        return parameter.ParameterType.IsByRef && !parameter.IsIn && !parameter.IsOut &&
               parameter.ParameterType.GetElementType() == type;
    }

    private static bool IsOut(ParameterInfo parameter, Type type)
    {
        return parameter.ParameterType.IsByRef && parameter.IsOut &&
               parameter.ParameterType.GetElementType() == type;
    }

    private static void AssignIds(List<EntryInfo> entries)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        for (int index = 0; index < entries.Count; index++)
        {
            EntryInfo entry = entries[index];
            entry.Id = index + 1;
            string name = string.Concat(entry.Key
                .Split(new[] { '.', '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
            if (name.Length == 0 || char.IsDigit(name[0]))
                name = "Source" + name;
            if (names.Contains(name))
                name = (entry.IsGet ? "Get" : "Set") + name;
            if (!names.Add(name))
                throw new InvalidOperationException($"Duplicate generated unit source enum name: {name}");
            entry.EnumName = name;
        }
    }

    private static string BuildSource(List<ProviderInfo> providers, List<EntryInfo> entries)
    {
        StringBuilder builder = new();
        builder.AppendLine("// AUTO-GENERATED - DO NOT EDIT MANUALLY");
        builder.AppendLine("// Use menu: Tools/Registry/Unit Sources");
        builder.AppendLine();
        builder.AppendLine("using System;");
        builder.AppendLine("using Unity.Burst;");
        builder.AppendLine("using Unity.Entities;");
        builder.AppendLine("using UnityEngine;");
        builder.AppendLine();
        AppendEnum(builder, entries);
        AppendRegistry(builder, providers, entries);
        AppendDispatcher(builder, entries);
        return builder.ToString().Replace("\r\n", "\n");
    }

    private static void AppendEnum(StringBuilder builder, List<EntryInfo> entries)
    {
        builder.AppendLine("public enum UnitSourceId : ushort");
        builder.AppendLine("{");
        builder.AppendLine("    None = 0,");
        for (int index = 0; index < entries.Count; index++)
            builder.AppendLine($"    {entries[index].EnumName} = {entries[index].Id},");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void AppendRegistry(
        StringBuilder builder,
        List<ProviderInfo> providers,
        List<EntryInfo> entries)
    {
        builder.AppendLine("public static class UnitComponentSourceRegistry");
        builder.AppendLine("{");
        builder.AppendLine("    public const string InteractionRequestKey = \"game.interaction.candidate\";");
        builder.AppendLine();
        AppendLookup(builder, entries.Where(entry => entry.IsGet), true);
        builder.AppendLine();
        AppendLookup(builder, entries.Where(entry => !entry.IsGet), false);
        builder.AppendLine();
        builder.AppendLine("    public static UnitSourceSchema CreateSchema(GameObject prefab, bool includeAll)");
        builder.AppendLine("    {");
        builder.AppendLine("        UnitSourceSchemaBuilder schema = new();");
        for (int providerIndex = 0; providerIndex < providers.Count; providerIndex++)
        {
            ProviderInfo provider = providers[providerIndex];
            string condition = provider.Attribute.IsGlobal
                ? "true"
                : provider.Attribute.AuthoringType != null
                    ? $"includeAll || prefab != null && prefab.GetComponentInChildren(typeof({TypeName(provider.Attribute.AuthoringType)}), true) != null"
                    : "includeAll";
            builder.AppendLine($"        if ({condition})");
            builder.AppendLine("        {");
            foreach (EntryInfo entry in entries.Where(entry => entry.Provider == provider))
            {
                string parameters = BuildParameters(entry);
                if (entry.IsGet)
                {
                    builder.AppendLine($"            schema.AddGet(\"{Escape(entry.Key)}\", typeof({TypeName(provider.Attribute.ComponentType)}), UnitValueCategory.{entry.Get.ReturnType}, {parameters});");
                }
                else
                {
                    builder.AppendLine($"            schema.AddSet(\"{Escape(entry.Key)}\", typeof({TypeName(provider.Attribute.ComponentType)}), {parameters}, {Bool(entry.Set.RequiresKey)});");
                }
            }

            if (provider.Type == typeof(GameInteractionSource))
                builder.AppendLine($"            schema.AddInteractionGet(InteractionRequestKey, typeof({TypeName(provider.Attribute.ComponentType)}));");
            builder.AppendLine("        }");
        }
        builder.AppendLine("        return schema.Build();");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    public static UnitSourceSchema CreateSchema() => CreateSchema(null, true);");
        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine("public static class UnitSourceSchemaFactory");
        builder.AppendLine("{");
        builder.AppendLine("    public static UnitSourceSchema CreateForAllSources() => UnitComponentSourceRegistry.CreateSchema(null, true);");
        builder.AppendLine("    public static UnitSourceSchema CreateForPrefab(GameObject prefab) => UnitComponentSourceRegistry.CreateSchema(prefab, false);");
        builder.AppendLine("}");
        builder.AppendLine();
    }

    private static void AppendLookup(StringBuilder builder, IEnumerable<EntryInfo> source, bool isGet)
    {
        string methodName = isGet ? "TryGetGet" : "TryGetSet";
        string schemaType = isGet ? "UnitSourceGetSchemaEntry" : "UnitSourceSetSchemaEntry";
        builder.AppendLine($"    public static bool {methodName}(string key, out UnitSourceId sourceId, out {schemaType} schema)");
        builder.AppendLine("    {");
        builder.AppendLine("        switch (key)");
        builder.AppendLine("        {");
        foreach (EntryInfo entry in source)
        {
            builder.AppendLine($"            case \"{Escape(entry.Key)}\":");
            builder.AppendLine($"                sourceId = UnitSourceId.{entry.EnumName};");
            if (isGet)
            {
                builder.AppendLine($"                schema = new UnitSourceGetSchemaEntry(\"{Escape(entry.Key)}\", typeof({TypeName(entry.Provider.Attribute.ComponentType)}), UnitValueCategory.{entry.Get.ReturnType}, {BuildParameters(entry)});");
            }
            else
            {
                builder.AppendLine($"                schema = new UnitSourceSetSchemaEntry(\"{Escape(entry.Key)}\", typeof({TypeName(entry.Provider.Attribute.ComponentType)}), {BuildParameters(entry)}, {Bool(entry.Set.RequiresKey)});");
            }
            builder.AppendLine("                return true;");
        }
        builder.AppendLine("            default:");
        builder.AppendLine("                sourceId = UnitSourceId.None;");
        builder.AppendLine("                schema = default;");
        builder.AppendLine("                return false;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
    }

    private static void AppendDispatcher(StringBuilder builder, List<EntryInfo> entries)
    {
        List<Type> lookupTypes = entries
            .Where(entry => entry.Mode is AccessMode.ComponentGet or AccessMode.ComponentSet or AccessMode.LookupGet)
            .Select(entry => entry.LookupType ?? entry.Provider.Attribute.ComponentType)
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();
        HashSet<Type> writableLookupTypes = entries
            .Where(entry => entry.Mode == AccessMode.ComponentSet)
            .Select(entry => entry.Provider.Attribute.ComponentType)
            .ToHashSet();

        builder.AppendLine("public struct UnitSourceDispatcher");
        builder.AppendLine("{");
        for (int index = 0; index < lookupTypes.Count; index++)
            builder.AppendLine($"    private ComponentLookup<{TypeName(lookupTypes[index])}> {FieldName(lookupTypes[index])};");
        builder.AppendLine();
        AppendInitialize(builder, lookupTypes, "SystemBase system", type =>
            $"system.GetComponentLookup<{TypeName(type)}>({Bool(!writableLookupTypes.Contains(type))})");
        AppendUpdate(builder, lookupTypes, "SystemBase system", type => $"{FieldName(type)}.Update(system)");
        AppendInitialize(builder, lookupTypes, "ref SystemState state", type =>
            $"state.GetComponentLookup<{TypeName(type)}>({Bool(!writableLookupTypes.Contains(type))})");
        AppendUpdate(builder, lookupTypes, "ref SystemState state", type => $"{FieldName(type)}.Update(ref state)");
        AppendTryGet(builder, entries);
        AppendTrySet(builder, entries);
        builder.AppendLine("    [BurstDiscard]");
        builder.AppendLine("    public void InvokeManagedInteraction(EntityManager entityManager, ref bool success, ref InteractionRequestSnapshot request)");
        builder.AppendLine("    {");
        builder.AppendLine("        success = GameInteractionSource.TryGetInteraction(entityManager, out request);");
        builder.AppendLine("    }");
        builder.AppendLine();
        AppendManagedGet(builder, entries);
        AppendManagedSet(builder, entries);
        builder.AppendLine("}");
    }

    private static void AppendInitialize(
        StringBuilder builder,
        List<Type> lookupTypes,
        string parameter,
        Func<Type, string> expression)
    {
        builder.AppendLine($"    public void Initialize({parameter})");
        builder.AppendLine("    {");
        for (int index = 0; index < lookupTypes.Count; index++)
            builder.AppendLine($"        {FieldName(lookupTypes[index])} = {expression(lookupTypes[index])};");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static void AppendUpdate(
        StringBuilder builder,
        List<Type> lookupTypes,
        string parameter,
        Func<Type, string> expression)
    {
        builder.AppendLine($"    public void Update({parameter})");
        builder.AppendLine("    {");
        for (int index = 0; index < lookupTypes.Count; index++)
            builder.AppendLine($"        {expression(lookupTypes[index])};");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static void AppendTryGet(StringBuilder builder, List<EntryInfo> entries)
    {
        builder.AppendLine("    public bool TryGet(Entity entity, UnitSourceId sourceId, in UnitSourceArguments arguments, out UnitSourceValue value)");
        builder.AppendLine("    {");
        builder.AppendLine("        value = default;");
        builder.AppendLine("        switch (sourceId)");
        builder.AppendLine("        {");
        foreach (EntryInfo entry in entries.Where(entry => entry.IsGet && entry.Mode == AccessMode.ComponentGet))
        {
            Type type = entry.Provider.Attribute.ComponentType;
            builder.AppendLine($"            case UnitSourceId.{entry.EnumName}:");
            builder.AppendLine("            {");
            builder.AppendLine($"                if (!{FieldName(type)}.TryGetComponent(entity, out {TypeName(type)} component))");
            builder.AppendLine("                    return false;");
            builder.AppendLine($"                return {TypeName(entry.Provider.Type)}.{entry.Method.Name}({entry.Operation}, in component, in arguments, out value);");
            builder.AppendLine("            }");
        }
        foreach (EntryInfo entry in entries.Where(entry => entry.IsGet && entry.Mode == AccessMode.LookupGet))
        {
            builder.AppendLine($"            case UnitSourceId.{entry.EnumName}:");
            builder.AppendLine($"                return {TypeName(entry.Provider.Type)}.{entry.Method.Name}({entry.Operation}, entity, in {FieldName(entry.LookupType)}, in arguments, out value);");
        }
        List<EntryInfo> managedEntries = entries
            .Where(entry => entry.IsGet && entry.Mode == AccessMode.ManagedGet)
            .ToList();
        if (managedEntries.Count > 0)
        {
            for (int index = 0; index < managedEntries.Count; index++)
                builder.AppendLine($"            case UnitSourceId.{managedEntries[index].EnumName}:");
            builder.AppendLine("                return false;");
        }
        builder.AppendLine("            default:");
        builder.AppendLine("                return false;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static void AppendTrySet(StringBuilder builder, List<EntryInfo> entries)
    {
        builder.AppendLine("    public bool TrySet(Entity entity, UnitSourceId sourceId, in UnitSourceArguments arguments)");
        builder.AppendLine("    {");
        builder.AppendLine("        switch (sourceId)");
        builder.AppendLine("        {");
        foreach (EntryInfo entry in entries.Where(entry => !entry.IsGet && entry.Mode == AccessMode.ComponentSet))
        {
            Type type = entry.Provider.Attribute.ComponentType;
            builder.AppendLine($"            case UnitSourceId.{entry.EnumName}:");
            builder.AppendLine("            {");
            builder.AppendLine($"                if (!{FieldName(type)}.HasComponent(entity))");
            builder.AppendLine("                    return false;");
            builder.AppendLine($"                RefRW<{TypeName(type)}> component = {FieldName(type)}.GetRefRW(entity);");
            builder.AppendLine($"                return {TypeName(entry.Provider.Type)}.{entry.Method.Name}({entry.Operation}, ref component.ValueRW, in arguments);");
            builder.AppendLine("            }");
        }
        List<EntryInfo> managedEntries = entries
            .Where(entry => !entry.IsGet && entry.Mode == AccessMode.ManagedSet)
            .ToList();
        if (managedEntries.Count > 0)
        {
            for (int index = 0; index < managedEntries.Count; index++)
                builder.AppendLine($"            case UnitSourceId.{managedEntries[index].EnumName}:");
            builder.AppendLine("                return false;");
        }
        builder.AppendLine("            default:");
        builder.AppendLine("                return false;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static void AppendManagedGet(StringBuilder builder, List<EntryInfo> entries)
    {
        builder.AppendLine("    [BurstDiscard]");
        builder.AppendLine("    public void InvokeManagedGet(EntityManager entityManager, UnitSourceId sourceId, Entity entity, in UnitSourceArguments arguments, ref bool success, ref UnitSourceValue value)");
        builder.AppendLine("    {");
        builder.AppendLine("        switch (sourceId)");
        builder.AppendLine("        {");
        foreach (EntryInfo entry in entries.Where(entry => entry.IsGet && entry.Mode == AccessMode.ManagedGet))
        {
            builder.AppendLine($"            case UnitSourceId.{entry.EnumName}:");
            builder.AppendLine($"                success = {TypeName(entry.Provider.Type)}.{entry.Method.Name}({entry.Operation}, entityManager, entity, in arguments, out value);");
            builder.AppendLine("                return;");
        }
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static void AppendManagedSet(StringBuilder builder, List<EntryInfo> entries)
    {
        builder.AppendLine("    [BurstDiscard]");
        builder.AppendLine("    public void InvokeManagedSet(EntityManager entityManager, UnitSourceId sourceId, Entity entity, in UnitSourceArguments arguments, ref bool success)");
        builder.AppendLine("    {");
        builder.AppendLine("        switch (sourceId)");
        builder.AppendLine("        {");
        foreach (EntryInfo entry in entries.Where(entry => !entry.IsGet && entry.Mode == AccessMode.ManagedSet))
        {
            builder.AppendLine($"            case UnitSourceId.{entry.EnumName}:");
            builder.AppendLine($"                success = {TypeName(entry.Provider.Type)}.{entry.Method.Name}({entry.Operation}, entityManager, entity, in arguments);");
            builder.AppendLine("                return;");
        }
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static string BuildParameters(EntryInfo entry)
    {
        if (entry.ParameterTypes.Length == 0)
            return "Array.Empty<ComparatorParameterDefinition>()";

        List<string> parameters = new(entry.ParameterTypes.Length);
        for (int index = 0; index < entry.ParameterTypes.Length; index++)
        {
            string name = entry.ParameterNames.Length == entry.ParameterTypes.Length
                ? entry.ParameterNames[index]
                : $"Value {index + 1}";
            parameters.Add($"new ComparatorParameterDefinition(\"{Escape(name)}\", UnitValueCategory.{entry.ParameterTypes[index]})");
        }
        return $"new[] {{ {string.Join(", ", parameters)} }}";
    }

    private static string TypeName(Type type)
    {
        return "global::" + type.FullName.Replace('+', '.');
    }

    private static string FieldName(Type type)
    {
        StringBuilder builder = new("_");
        string name = type.FullName ?? type.Name;
        for (int index = 0; index < name.Length; index++)
            builder.Append(char.IsLetterOrDigit(name[index]) ? name[index] : '_');
        builder.Append("Lookup");
        return builder.ToString();
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string Bool(bool value) => value ? "true" : "false";
}
#endif
