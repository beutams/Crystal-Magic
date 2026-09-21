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
        LookupSet,
        Unsupported,
    }

    private enum LookupKind
    {
        Component,
        Buffer,
    }

    private sealed class LookupParameterInfo
    {
        public LookupKind Kind;
        public Type ElementType;
        public bool IsReadOnly;
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
        public IReadOnlyList<LookupParameterInfo> LookupParameters;
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
                IReadOnlyList<LookupParameterInfo> lookupParameters = ResolveLookupParameters(method);
                AccessMode getMode = ResolveAccessMode(provider, method, true, lookupParameters);
                AccessMode setMode = ResolveAccessMode(provider, method, false, lookupParameters);
                for (int index = 0; index < gets.Length; index++)
                {
                    entries.Add(new EntryInfo
                    {
                        Provider = provider,
                        Method = method,
                        Get = gets[index],
                        Mode = getMode,
                        LookupParameters = lookupParameters,
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
                        LookupParameters = lookupParameters,
                    });
                }
            }
        }

        return entries
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ThenBy(entry => entry.IsGet ? 0 : 1)
            .ToList();
    }

    private static AccessMode ResolveAccessMode(
        ProviderInfo provider,
        MethodInfo method,
        bool isGet,
        IReadOnlyList<LookupParameterInfo> lookupParameters)
    {
        ParameterInfo[] parameters = method.GetParameters();
        if (parameters.Any(parameter => parameter.ParameterType == typeof(EntityManager)))
            return AccessMode.Unsupported;

        if (lookupParameters.Count > 0)
            return isGet ? AccessMode.LookupGet : AccessMode.LookupSet;

        // ComponentLookup<T> only accepts unmanaged component data. Managed component
        // providers remain in the generated schema, but have no runtime fallback.
        if (!provider.Attribute.ComponentType.IsValueType)
            return AccessMode.Unsupported;

        return isGet ? AccessMode.ComponentGet : AccessMode.ComponentSet;
    }

    private static IReadOnlyList<LookupParameterInfo> ResolveLookupParameters(MethodInfo method)
    {
        List<LookupParameterInfo> result = new();
        ParameterInfo[] parameters = method.GetParameters();
        for (int index = 0; index < parameters.Length; index++)
        {
            ParameterInfo parameter = parameters[index];
            if (!parameter.ParameterType.IsByRef)
                continue;

            Type parameterType = parameter.ParameterType.GetElementType();
            if (parameterType?.IsGenericType != true)
                continue;

            Type genericType = parameterType.GetGenericTypeDefinition();
            LookupKind kind;
            if (genericType == typeof(ComponentLookup<>))
                kind = LookupKind.Component;
            else if (genericType == typeof(BufferLookup<>))
                kind = LookupKind.Buffer;
            else
                continue;

            result.Add(new LookupParameterInfo
            {
                Kind = kind,
                ElementType = parameterType.GetGenericArguments()[0],
                IsReadOnly = parameter.IsIn,
            });
        }

        return result;
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
        if (entry.Mode == AccessMode.Unsupported)
            return;

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
            AccessMode.LookupGet => ValidateLookupSignature(entry, parameters, true),
            AccessMode.LookupSet => ValidateLookupSignature(entry, parameters, false),
            _ => false,
        };

        if (!valid)
            throw new InvalidOperationException($"Unsupported unit source signature: {entry.Provider.Type.FullName}.{entry.Method.Name}");
    }

    private static bool ValidateLookupSignature(EntryInfo entry, ParameterInfo[] parameters, bool isGet)
    {
        int lookupCount = entry.LookupParameters.Count;
        int expectedLength = lookupCount + (isGet ? 4 : 3);
        if (parameters.Length != expectedLength ||
            parameters[0].ParameterType != typeof(int) ||
            parameters[1].ParameterType != typeof(Entity) &&
            parameters[1].ParameterType != typeof(UnitSourceAccessContext))
        {
            return false;
        }

        for (int index = 0; index < lookupCount; index++)
        {
            LookupParameterInfo lookup = entry.LookupParameters[index];
            Type lookupType = lookup.Kind == LookupKind.Component
                ? typeof(ComponentLookup<>).MakeGenericType(lookup.ElementType)
                : typeof(BufferLookup<>).MakeGenericType(lookup.ElementType);
            ParameterInfo parameter = parameters[index + 2];
            bool valid = lookup.IsReadOnly
                ? IsReadOnlyByRef(parameter, lookupType)
                : IsWritableByRef(parameter, lookupType);
            if (!valid || isGet && !lookup.IsReadOnly)
                return false;
        }

        int argumentsIndex = lookupCount + 2;
        if (!IsReadOnlyByRef(parameters[argumentsIndex], typeof(UnitSourceArguments)))
            return false;

        return !isGet || IsOut(parameters[argumentsIndex + 1], typeof(UnitSourceValue));
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
        builder.AppendLine("using Unity.Collections;");
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
        List<Type> componentLookupTypes = entries
            .Where(entry => entry.Mode is AccessMode.ComponentGet or AccessMode.ComponentSet)
            .Select(entry => entry.Provider.Attribute.ComponentType)
            .Concat(entries
                .Where(entry => entry.Mode is AccessMode.LookupGet or AccessMode.LookupSet)
                .SelectMany(entry => entry.LookupParameters)
                .Where(lookup => lookup.Kind == LookupKind.Component)
                .Select(lookup => lookup.ElementType))
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();
        List<Type> bufferLookupTypes = entries
            .Where(entry => entry.Mode is AccessMode.LookupGet or AccessMode.LookupSet)
            .SelectMany(entry => entry.LookupParameters)
            .Where(lookup => lookup.Kind == LookupKind.Buffer)
            .Select(lookup => lookup.ElementType)
            .Distinct()
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();
        HashSet<Type> writableComponentLookupTypes = entries
            .Where(entry => entry.Mode == AccessMode.ComponentSet)
            .Select(entry => entry.Provider.Attribute.ComponentType)
            .Concat(entries
                .Where(entry => entry.Mode == AccessMode.LookupSet)
                .SelectMany(entry => entry.LookupParameters)
                .Where(lookup => lookup.Kind == LookupKind.Component && !lookup.IsReadOnly)
                .Select(lookup => lookup.ElementType))
            .ToHashSet();
        HashSet<Type> writableBufferLookupTypes = entries
            .Where(entry => entry.Mode == AccessMode.LookupSet)
            .SelectMany(entry => entry.LookupParameters)
            .Where(lookup => lookup.Kind == LookupKind.Buffer && !lookup.IsReadOnly)
            .Select(lookup => lookup.ElementType)
            .ToHashSet();

        builder.AppendLine("public struct UnitSourceDispatcher");
        builder.AppendLine("{");
        builder.AppendLine("    private Entity _globalEntity;");
        for (int index = 0; index < componentLookupTypes.Count; index++)
        {
            if (!writableComponentLookupTypes.Contains(componentLookupTypes[index]))
                builder.AppendLine("    [ReadOnly]");
            builder.AppendLine($"    private ComponentLookup<{TypeName(componentLookupTypes[index])}> {ComponentFieldName(componentLookupTypes[index])};");
        }
        for (int index = 0; index < bufferLookupTypes.Count; index++)
        {
            if (!writableBufferLookupTypes.Contains(bufferLookupTypes[index]))
                builder.AppendLine("    [ReadOnly]");
            builder.AppendLine($"    private BufferLookup<{TypeName(bufferLookupTypes[index])}> {BufferFieldName(bufferLookupTypes[index])};");
        }
        builder.AppendLine();
        AppendInitialize(
            builder,
            "Initialize",
            componentLookupTypes,
            bufferLookupTypes,
            "SystemBase system",
            "system.EntityManager",
            type => $"system.GetComponentLookup<{TypeName(type)}>({Bool(!writableComponentLookupTypes.Contains(type))})",
            type => $"system.GetBufferLookup<{TypeName(type)}>({Bool(!writableBufferLookupTypes.Contains(type))})");
        AppendInitialize(
            builder,
            "InitializeReadOnly",
            componentLookupTypes,
            bufferLookupTypes,
            "SystemBase system",
            "system.EntityManager",
            type => $"system.GetComponentLookup<{TypeName(type)}>(true)",
            type => $"system.GetBufferLookup<{TypeName(type)}>(true)");
        AppendUpdate(builder, componentLookupTypes, bufferLookupTypes, "SystemBase system", "system");
        AppendInitialize(
            builder,
            "Initialize",
            componentLookupTypes,
            bufferLookupTypes,
            "ref SystemState state",
            "state.EntityManager",
            type => $"state.GetComponentLookup<{TypeName(type)}>({Bool(!writableComponentLookupTypes.Contains(type))})",
            type => $"state.GetBufferLookup<{TypeName(type)}>({Bool(!writableBufferLookupTypes.Contains(type))})");
        AppendInitialize(
            builder,
            "InitializeReadOnly",
            componentLookupTypes,
            bufferLookupTypes,
            "ref SystemState state",
            "state.EntityManager",
            type => $"state.GetComponentLookup<{TypeName(type)}>(true)",
            type => $"state.GetBufferLookup<{TypeName(type)}>(true)");
        AppendUpdate(builder, componentLookupTypes, bufferLookupTypes, "ref SystemState state", "ref state");
        AppendTryGet(builder, entries);
        AppendTrySet(builder, entries);
        builder.AppendLine("    public bool TryGetInteraction(out InteractionRequestSnapshot request)");
        builder.AppendLine("    {");
        builder.AppendLine("        request = default;");
        builder.AppendLine($"        if (!{ComponentFieldName(typeof(InteractionCandidateComponent))}.TryGetComponent(_globalEntity, out InteractionCandidateComponent candidate))");
        builder.AppendLine("            return false;");
        builder.AppendLine("        return GameInteractionSource.TryGetInteraction(in candidate, out request);");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("}");
    }

    private static void AppendInitialize(
        StringBuilder builder,
        string methodName,
        List<Type> componentLookupTypes,
        List<Type> bufferLookupTypes,
        string parameter,
        string entityManagerExpression,
        Func<Type, string> componentExpression,
        Func<Type, string> bufferExpression)
    {
        builder.AppendLine($"    public void {methodName}({parameter})");
        builder.AppendLine("    {");
        for (int index = 0; index < componentLookupTypes.Count; index++)
            builder.AppendLine($"        {ComponentFieldName(componentLookupTypes[index])} = {componentExpression(componentLookupTypes[index])};");
        for (int index = 0; index < bufferLookupTypes.Count; index++)
            builder.AppendLine($"        {BufferFieldName(bufferLookupTypes[index])} = {bufferExpression(bufferLookupTypes[index])};");
        builder.AppendLine($"        if (_globalEntity == Entity.Null || !{entityManagerExpression}.Exists(_globalEntity))");
        builder.AppendLine($"            WorldStateUtility.TryGetEntity({entityManagerExpression}, out _globalEntity);");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static void AppendUpdate(
        StringBuilder builder,
        List<Type> componentLookupTypes,
        List<Type> bufferLookupTypes,
        string parameter,
        string updateArgument)
    {
        builder.AppendLine($"    public void Update({parameter})");
        builder.AppendLine("    {");
        for (int index = 0; index < componentLookupTypes.Count; index++)
            builder.AppendLine($"        {ComponentFieldName(componentLookupTypes[index])}.Update({updateArgument});");
        for (int index = 0; index < bufferLookupTypes.Count; index++)
            builder.AppendLine($"        {BufferFieldName(bufferLookupTypes[index])}.Update({updateArgument});");
        string entityManagerExpression = parameter.StartsWith("ref ", StringComparison.Ordinal)
            ? "state.EntityManager"
            : "system.EntityManager";
        builder.AppendLine($"        if (_globalEntity == Entity.Null || !{entityManagerExpression}.Exists(_globalEntity))");
        builder.AppendLine($"            WorldStateUtility.TryGetEntity({entityManagerExpression}, out _globalEntity);");
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
            string target = entry.Provider.Attribute.IsGlobal ? "_globalEntity" : "entity";
            builder.AppendLine($"            case UnitSourceId.{entry.EnumName}:");
            builder.AppendLine("            {");
            builder.AppendLine($"                if (!{ComponentFieldName(type)}.TryGetComponent({target}, out {TypeName(type)} component))");
            builder.AppendLine("                    return false;");
            builder.AppendLine($"                return {TypeName(entry.Provider.Type)}.{entry.Method.Name}({entry.Operation}, in component, in arguments, out value);");
            builder.AppendLine("            }");
        }
        foreach (EntryInfo entry in entries.Where(entry => entry.IsGet && entry.Mode == AccessMode.LookupGet))
        {
            string target = entry.Provider.Attribute.IsGlobal ? "_globalEntity" : "entity";
            builder.AppendLine($"            case UnitSourceId.{entry.EnumName}:");
            builder.AppendLine($"                return {TypeName(entry.Provider.Type)}.{entry.Method.Name}({entry.Operation}, {BuildTargetArgument(entry, target)}, {BuildLookupArguments(entry)}, in arguments, out value);");
        }
        List<EntryInfo> unsupportedEntries = entries
            .Where(entry => entry.IsGet && entry.Mode == AccessMode.Unsupported)
            .ToList();
        if (unsupportedEntries.Count > 0)
        {
            for (int index = 0; index < unsupportedEntries.Count; index++)
                builder.AppendLine($"            case UnitSourceId.{unsupportedEntries[index].EnumName}:");
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
            string target = entry.Provider.Attribute.IsGlobal ? "_globalEntity" : "entity";
            builder.AppendLine($"            case UnitSourceId.{entry.EnumName}:");
            builder.AppendLine("            {");
            builder.AppendLine($"                if (!{ComponentFieldName(type)}.HasComponent({target}))");
            builder.AppendLine("                    return false;");
            builder.AppendLine($"                RefRW<{TypeName(type)}> component = {ComponentFieldName(type)}.GetRefRW({target});");
            builder.AppendLine($"                return {TypeName(entry.Provider.Type)}.{entry.Method.Name}({entry.Operation}, ref component.ValueRW, in arguments);");
            builder.AppendLine("            }");
        }
        foreach (EntryInfo entry in entries.Where(entry => !entry.IsGet && entry.Mode == AccessMode.LookupSet))
        {
            string target = entry.Provider.Attribute.IsGlobal ? "_globalEntity" : "entity";
            builder.AppendLine($"            case UnitSourceId.{entry.EnumName}:");
            builder.AppendLine($"                return {TypeName(entry.Provider.Type)}.{entry.Method.Name}({entry.Operation}, {BuildTargetArgument(entry, target)}, {BuildLookupArguments(entry)}, in arguments);");
        }
        List<EntryInfo> unsupportedEntries = entries
            .Where(entry => !entry.IsGet && entry.Mode == AccessMode.Unsupported)
            .ToList();
        if (unsupportedEntries.Count > 0)
        {
            for (int index = 0; index < unsupportedEntries.Count; index++)
                builder.AppendLine($"            case UnitSourceId.{unsupportedEntries[index].EnumName}:");
            builder.AppendLine("                return false;");
        }
        builder.AppendLine("            default:");
        builder.AppendLine("                return false;");
        builder.AppendLine("        }");
        builder.AppendLine("    }");
        builder.AppendLine();
    }

    private static string BuildLookupArguments(EntryInfo entry)
    {
        return string.Join(", ", entry.LookupParameters.Select(lookup =>
            $"{(lookup.IsReadOnly ? "in" : "ref")} {(lookup.Kind == LookupKind.Component ? ComponentFieldName(lookup.ElementType) : BufferFieldName(lookup.ElementType))}"));
    }

    private static string BuildTargetArgument(EntryInfo entry, string target)
    {
        return entry.Method.GetParameters()[1].ParameterType == typeof(UnitSourceAccessContext)
            ? $"new UnitSourceAccessContext({target}, _globalEntity)"
            : target;
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

    private static string ComponentFieldName(Type type)
    {
        StringBuilder builder = new("_");
        string name = type.FullName ?? type.Name;
        for (int index = 0; index < name.Length; index++)
            builder.Append(char.IsLetterOrDigit(name[index]) ? name[index] : '_');
        builder.Append("Lookup");
        return builder.ToString();
    }

    private static string BufferFieldName(Type type)
    {
        StringBuilder builder = new("_");
        string name = type.FullName ?? type.Name;
        for (int index = 0; index < name.Length; index++)
            builder.Append(char.IsLetterOrDigit(name[index]) ? name[index] : '_');
        builder.Append("BufferLookup");
        return builder.ToString();
    }

    private static string Escape(string value)
    {
        return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    private static string Bool(bool value) => value ? "true" : "false";
}
#endif
