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
            "PositionSkill",
            "SelfSkill",
        };

        private static readonly Dictionary<string, Type> s_skillRuntimeTypes = new(StringComparer.Ordinal)
        {
            { "PositionSkill", typeof(PositionSkill) },
            { "SelfSkill", typeof(SelfSkill) },
        };

        private static readonly Dictionary<Type, string> s_skillRuntimeKeys = new()
        {
            { typeof(PositionSkill), "PositionSkill" },
            { typeof(SelfSkill), "SelfSkill" },
        };

        private static readonly Dictionary<string, string> s_skillRuntimeDisplayNames = new(StringComparer.Ordinal)
        {
            { "PositionSkill", "Position Skill" },
            { "SelfSkill", "Self Skill" },
        };

        private static readonly FactoryTypeInfo[] s_skillRuntimeTypeInfos =
        {
            new("PositionSkill", "Position Skill", typeof(PositionSkill), 0),
            new("SelfSkill", "Self Skill", typeof(SelfSkill), 10),
        };

        public static string DefaultSkillRuntimeTypeKey => "PositionSkill";

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

            factory.Register("PositionSkill", static data => new PositionSkill(data));
            factory.Register("SelfSkill", static data => new SelfSkill(data));
        }
    }
}
