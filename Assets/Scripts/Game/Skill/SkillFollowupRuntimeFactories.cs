namespace CrystalMagic.Game.Skill
{
    public static class SkillFollowupRuntimeFactories
    {
        public static readonly SkillFollowupFilterFactory FilterFactory = CreateFilterFactory();
        public static readonly SkillFollowupConsumeRuleFactory ConsumeRuleFactory = CreateConsumeRuleFactory();
        public static readonly SkillFollowupModifierRuleFactory ModifierRuleFactory = CreateModifierRuleFactory();

        private static SkillFollowupFilterFactory CreateFilterFactory()
        {
            SkillFollowupFilterFactory factory = new();
            SkillFollowupFilterRegistry.RegisterAll(factory);
            return factory;
        }

        private static SkillFollowupConsumeRuleFactory CreateConsumeRuleFactory()
        {
            SkillFollowupConsumeRuleFactory factory = new();
            SkillFollowupConsumeRuleRegistry.RegisterAll(factory);
            return factory;
        }

        private static SkillFollowupModifierRuleFactory CreateModifierRuleFactory()
        {
            SkillFollowupModifierRuleFactory factory = new();
            SkillFollowupModifierRuleRegistry.RegisterAll(factory);
            return factory;
        }
    }
}
