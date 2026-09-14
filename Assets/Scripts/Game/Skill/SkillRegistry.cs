// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Use menu: Tools/Registry/Skill Runtime

using System;
using System.Collections.Generic;

namespace CrystalMagic.Game.Skill
{
    public static class SkillRegistry
    {
        private static readonly string[] s_skillRuntimeTypeOrder =
        {
            "CommonSkill",
        };

        private static readonly Dictionary<string, Type> s_skillRuntimeTypes = new(StringComparer.Ordinal)
        {
            { "CommonSkill", typeof(CommonSkill) },
        };

        private static readonly Dictionary<Type, string> s_skillRuntimeKeys = new()
        {
            { typeof(CommonSkill), "CommonSkill" },
        };

        private static readonly Dictionary<string, string> s_skillRuntimeDisplayNames = new(StringComparer.Ordinal)
        {
            { "CommonSkill", "Common Skill" },
        };

        private static readonly FactoryTypeInfo[] s_skillRuntimeTypeInfos =
        {
            new("CommonSkill", "Common Skill", typeof(CommonSkill), 0),
        };

        public static string DefaultSkillRuntimeTypeKey => "CommonSkill";

        public static IReadOnlyList<string> SkillRuntimeTypeOrder => s_skillRuntimeTypeOrder;

        public static IReadOnlyList<FactoryTypeInfo> SkillRuntimeTypeInfos => s_skillRuntimeTypeInfos;

        public static bool ContainsSkillRuntimeKey(string key)
        {
            return s_skillRuntimeTypes.ContainsKey(key ?? string.Empty);
        }

        public static bool TryGetSkillRuntimeType(string key, out Type type)
        {
            return s_skillRuntimeTypes.TryGetValue(key ?? string.Empty, out type);
        }

        public static bool TryGetSkillRuntimeKey(Type type, out string key)
        {
            return s_skillRuntimeKeys.TryGetValue(type, out key);
        }

        public static string GetSkillRuntimeDisplayName(string key)
        {
            return s_skillRuntimeDisplayNames.TryGetValue(key ?? string.Empty, out string displayName)
                ? displayName
                : key ?? "Unknown";
        }

        public static void RegisterAll(SkillFactory factory)
        {
            if (factory == null)
                return;

            factory.Register("CommonSkill", static data => new CommonSkill(data));
        }
    }
}
