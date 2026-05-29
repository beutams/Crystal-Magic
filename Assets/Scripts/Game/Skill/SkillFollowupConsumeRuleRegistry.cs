// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Use menu: Tools/Registry/Skill Followup Consume Rule

using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Skill
{
    public static class SkillFollowupConsumeRuleRegistry
    {
        private static readonly string[] s_ruleKeyOrder =
        {
            "UseCount",
        };

        private static readonly Dictionary<string, Type> s_ruleDataTypes = new(StringComparer.Ordinal)
        {
            { "UseCount", typeof(UseCountSkillFollowupConsumeRuleData) },
        };

        private static readonly Dictionary<string, Type> s_ruleRuntimeTypes = new(StringComparer.Ordinal)
        {
            { "UseCount", typeof(UseCountSkillFollowupConsumeRule) },
        };

        private static readonly Dictionary<Type, string> s_ruleDataKeys = new()
        {
            { typeof(UseCountSkillFollowupConsumeRuleData), "UseCount" },
        };

        private static readonly Dictionary<Type, string> s_ruleRuntimeKeys = new()
        {
            { typeof(UseCountSkillFollowupConsumeRule), "UseCount" },
        };

        private static readonly Dictionary<string, string> s_ruleDisplayNames = new(StringComparer.Ordinal)
        {
            { "UseCount", "Use Count" },
        };

        private static readonly FactoryTypeInfo[] s_ruleTypeInfos =
        {
            new("UseCount", "Use Count", typeof(UseCountSkillFollowupConsumeRuleData), 0),
        };

        public static string DefaultRuleKey => "UseCount";

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

        public static string GetRuleKey(SkillFollowupConsumeRuleData ruleData)
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

        public static SkillFollowupConsumeRuleData CreateRuleData(string key)
        {
            if (!TryGetRuleDataType(key, out Type type))
                return null;

            return Activator.CreateInstance(type) as SkillFollowupConsumeRuleData;
        }

        public static SkillFollowupConsumeRule CreateRule(string key)
        {
            return s_factory.Create(key ?? string.Empty);
        }

        public static bool TryCreateRule(string key, out SkillFollowupConsumeRule rule)
        {
            return s_factory.TryCreate(key ?? string.Empty, out rule);
        }

        private static readonly SkillFollowupConsumeRuleFactory s_factory = CreateFactory();

        private static SkillFollowupConsumeRuleFactory CreateFactory()
        {
            SkillFollowupConsumeRuleFactory factory = new();
            RegisterAll(factory);
            return factory;
        }

        public static void RegisterAll(SkillFollowupConsumeRuleFactory factory)
        {
            if (factory == null)
                return;

            factory.Register("UseCount", static () => new UseCountSkillFollowupConsumeRule());
        }
    }
}
