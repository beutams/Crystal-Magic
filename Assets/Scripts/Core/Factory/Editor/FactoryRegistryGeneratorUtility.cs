using System;
using System.Collections.Generic;
using System.Reflection;

namespace CrystalMagic.Editor
{
    public static class FactoryRegistryGeneratorUtility
    {
        public sealed class MappedType
        {
            public MappedType(Type type, FactoryKeyAttribute mapping)
            {
                Type = type;
                Mapping = mapping;
            }

            public Type Type { get; }
            public FactoryKeyAttribute Mapping { get; }
        }

        public static List<MappedType> CollectMappedTypes(Type baseType, bool subclassOnly)
        {
            List<Type> types = RegistryGeneratorUtility.CollectTypes(baseType, subclassOnly);
            List<MappedType> result = new();

            for (int i = 0; i < types.Count; i++)
            {
                Type type = types[i];
                FactoryKeyAttribute mapping = type.GetCustomAttribute<FactoryKeyAttribute>(false);
                if (mapping == null || string.IsNullOrWhiteSpace(mapping.Key))
                    continue;

                result.Add(new MappedType(type, mapping));
            }

            result.Sort(CompareMappedTypes);
            return result;
        }

        public static string TypeReference(Type type)
        {
            return RegistryGeneratorUtility.GetFriendlyTypeName(type);
        }

        public static string DisplayName(MappedType mappedType)
        {
            return string.IsNullOrWhiteSpace(mappedType.Mapping.DisplayName)
                ? mappedType.Mapping.Key
                : mappedType.Mapping.DisplayName;
        }

        public static string Literal(string value)
        {
            value ??= string.Empty;
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        public static bool HasWritableFloatMember(Type type, string memberName)
        {
            if (string.IsNullOrWhiteSpace(memberName))
                return false;

            FieldInfo field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
            if (field != null)
                return field.FieldType == typeof(float) && !field.IsInitOnly;

            PropertyInfo property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
            return property != null && property.PropertyType == typeof(float) && property.CanWrite;
        }

        private static int CompareMappedTypes(MappedType left, MappedType right)
        {
            int order = left.Mapping.Order.CompareTo(right.Mapping.Order);
            if (order != 0)
                return order;

            int key = string.Compare(left.Mapping.Key, right.Mapping.Key, StringComparison.Ordinal);
            if (key != 0)
                return key;

            return string.Compare(left.Type.Name, right.Type.Name, StringComparison.Ordinal);
        }
    }
}
