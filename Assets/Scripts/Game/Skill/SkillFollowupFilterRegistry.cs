// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Use menu: Tools/Registry/Skill Followup Filter

using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Skill
{
    public static class SkillFollowupFilterRegistry
    {
        private static readonly string[] s_filterKeyOrder =
        {
            "AnySkill",
            "SkillId",
            "RuntimeType",
            "Element",
            "SkillAdditionName",
        };

        private static readonly Dictionary<string, Type> s_filterDataTypes = new(StringComparer.Ordinal)
        {
            { "AnySkill", typeof(AnySkillFollowupFilterData) },
            { "SkillId", typeof(SkillIdFollowupFilterData) },
            { "RuntimeType", typeof(RuntimeTypeFollowupFilterData) },
            { "Element", typeof(ElementFollowupFilterData) },
            { "SkillAdditionName", typeof(SkillAdditionNameFollowupFilterData) },
        };

        private static readonly Dictionary<string, Type> s_filterRuntimeTypes = new(StringComparer.Ordinal)
        {
            { "AnySkill", typeof(AnySkillFollowupFilter) },
            { "SkillId", typeof(SkillIdFollowupFilter) },
            { "RuntimeType", typeof(RuntimeTypeFollowupFilter) },
            { "Element", typeof(ElementFollowupFilter) },
            { "SkillAdditionName", typeof(SkillAdditionNameFollowupFilter) },
        };

        private static readonly Dictionary<Type, string> s_filterDataKeys = new()
        {
            { typeof(AnySkillFollowupFilterData), "AnySkill" },
            { typeof(SkillIdFollowupFilterData), "SkillId" },
            { typeof(RuntimeTypeFollowupFilterData), "RuntimeType" },
            { typeof(ElementFollowupFilterData), "Element" },
            { typeof(SkillAdditionNameFollowupFilterData), "SkillAdditionName" },
        };

        private static readonly Dictionary<Type, string> s_filterRuntimeKeys = new()
        {
            { typeof(AnySkillFollowupFilter), "AnySkill" },
            { typeof(SkillIdFollowupFilter), "SkillId" },
            { typeof(RuntimeTypeFollowupFilter), "RuntimeType" },
            { typeof(ElementFollowupFilter), "Element" },
            { typeof(SkillAdditionNameFollowupFilter), "SkillAdditionName" },
        };

        private static readonly Dictionary<string, string> s_filterDisplayNames = new(StringComparer.Ordinal)
        {
            { "AnySkill", "Any Skill" },
            { "SkillId", "Skill Id" },
            { "RuntimeType", "Runtime Type" },
            { "Element", "Element" },
            { "SkillAdditionName", "Skill Addition Name" },
        };

        private static readonly FactoryTypeInfo[] s_filterTypeInfos =
        {
            new("AnySkill", "Any Skill", typeof(AnySkillFollowupFilterData), 0),
            new("SkillId", "Skill Id", typeof(SkillIdFollowupFilterData), 10),
            new("RuntimeType", "Runtime Type", typeof(RuntimeTypeFollowupFilterData), 20),
            new("Element", "Element", typeof(ElementFollowupFilterData), 30),
            new("SkillAdditionName", "Skill Addition Name", typeof(SkillAdditionNameFollowupFilterData), 40),
        };

        public static string DefaultFilterKey => "AnySkill";

        public static IReadOnlyList<string> FilterKeyOrder => s_filterKeyOrder;

        public static IReadOnlyList<FactoryTypeInfo> FilterTypeInfos => s_filterTypeInfos;

        public static bool TryGetFilterDataType(string key, out Type type)
        {
            return s_filterDataTypes.TryGetValue(key ?? string.Empty, out type);
        }

        public static bool TryGetFilterRuntimeType(string key, out Type type)
        {
            return s_filterRuntimeTypes.TryGetValue(key ?? string.Empty, out type);
        }

        public static bool TryGetFilterKey(Type type, out string key)
        {
            if (type != null && s_filterDataKeys.TryGetValue(type, out key))
                return true;

            if (type != null && s_filterRuntimeKeys.TryGetValue(type, out key))
                return true;

            key = null;
            return false;
        }

        public static string GetFilterKey(SkillFollowupFilterData filterData)
        {
            return filterData != null && s_filterDataKeys.TryGetValue(filterData.GetType(), out string key)
                ? key
                : string.Empty;
        }

        public static string GetDisplayName(string key)
        {
            return s_filterDisplayNames.TryGetValue(key ?? string.Empty, out string displayName)
                ? displayName
                : key ?? "Unknown";
        }

        public static SkillFollowupFilterData CreateFilterData(string key)
        {
            if (!TryGetFilterDataType(key, out Type type))
                return null;

            return Activator.CreateInstance(type) as SkillFollowupFilterData;
        }

        public static void RegisterAll(SkillFollowupFilterFactory factory)
        {
            if (factory == null)
                return;

            factory.Register("AnySkill", static () => new AnySkillFollowupFilter());
            factory.Register("SkillId", static () => new SkillIdFollowupFilter());
            factory.Register("RuntimeType", static () => new RuntimeTypeFollowupFilter());
            factory.Register("Element", static () => new ElementFollowupFilter());
            factory.Register("SkillAdditionName", static () => new SkillAdditionNameFollowupFilter());
        }
    }
}
