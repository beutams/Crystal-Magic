// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Use menu: Tools/Registry/Skill Followup Modifier Rule

using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Skill
{
    public static class SkillFollowupModifierRuleRegistry
    {
        private static readonly string[] s_ruleKeyOrder =
        {
            "Static",
            "Sequence",
        };

        private static readonly Dictionary<string, Type> s_ruleDataTypes = new(StringComparer.Ordinal)
        {
            { "Static", typeof(StaticSkillFollowupModifierRuleData) },
            { "Sequence", typeof(SequenceSkillFollowupModifierRuleData) },
        };

        private static readonly Dictionary<string, Type> s_ruleRuntimeTypes = new(StringComparer.Ordinal)
        {
            { "Static", typeof(StaticSkillFollowupModifierRule) },
            { "Sequence", typeof(SequenceSkillFollowupModifierRule) },
        };

        private static readonly Dictionary<Type, string> s_ruleDataKeys = new()
        {
            { typeof(StaticSkillFollowupModifierRuleData), "Static" },
            { typeof(SequenceSkillFollowupModifierRuleData), "Sequence" },
        };

        private static readonly Dictionary<Type, string> s_ruleRuntimeKeys = new()
        {
            { typeof(StaticSkillFollowupModifierRule), "Static" },
            { typeof(SequenceSkillFollowupModifierRule), "Sequence" },
        };

        private static readonly Dictionary<string, string> s_ruleDisplayNames = new(StringComparer.Ordinal)
        {
            { "Static", "Static" },
            { "Sequence", "Sequence" },
        };

        private static readonly FactoryTypeInfo[] s_ruleTypeInfos =
        {
            new("Static", "Static", typeof(StaticSkillFollowupModifierRuleData), 0),
            new("Sequence", "Sequence", typeof(SequenceSkillFollowupModifierRuleData), 10),
        };

        public static string DefaultRuleKey => "Static";

        public static IReadOnlyList<string> RuleKeyOrder => s_ruleKeyOrder;

        public static IReadOnlyList<FactoryTypeInfo> RuleTypeInfos => s_ruleTypeInfos;

        public static bool TryGetRuleDataType(string key, out Type type)
        {
            return s_ruleDataTypes.TryGetValue(key ?? string.Empty, out type);
        }

        public static bool TryGetRuleRuntimeType(string key, out Type type)
        {
            return s_ruleRuntimeTypes.TryGetValue(key ?? string.Empty, out type);
        }

        public static bool TryGetRuleKey(Type type, out string key)
        {
            if (type != null && s_ruleDataKeys.TryGetValue(type, out key))
                return true;

            if (type != null && s_ruleRuntimeKeys.TryGetValue(type, out key))
                return true;

            key = null;
            return false;
        }

        public static string GetRuleKey(SkillFollowupModifierRuleData ruleData)
        {
            return ruleData != null && s_ruleDataKeys.TryGetValue(ruleData.GetType(), out string key)
                ? key
                : string.Empty;
        }

        public static string GetDisplayName(string key)
        {
            return s_ruleDisplayNames.TryGetValue(key ?? string.Empty, out string displayName)
                ? displayName
                : key ?? "Unknown";
        }

        public static SkillFollowupModifierRuleData CreateRuleData(string key)
        {
            if (!TryGetRuleDataType(key, out Type type))
                return null;

            return Activator.CreateInstance(type) as SkillFollowupModifierRuleData;
        }

        public static void RegisterAll(SkillFollowupModifierRuleFactory factory)
        {
            if (factory == null)
                return;

            factory.Register("Static", static () => new StaticSkillFollowupModifierRule());
            factory.Register("Sequence", static () => new SequenceSkillFollowupModifierRule());
        }
    }
}
