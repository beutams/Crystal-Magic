// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Use menu: Tools/Registry/Skill Addition Action

using CrystalMagic.Game.Data;

namespace CrystalMagic.Game.Skill
{
    public static class SkillAdditionActionRegistry
    {
        private static readonly SkillAdditionActionFactory s_factory = CreateFactory();

        public static SkillAdditionAction Create(SkillAdditionActionData data, SkillAdditionActionContext context)
        {
            return s_factory.CreateAction(data, context);
        }

        private static SkillAdditionActionFactory CreateFactory()
        {
            SkillAdditionActionFactory factory = new();
            factory.Register(typeof(ModifyCurrentSkillAdditionActionData), static request => new ModifyCurrentSkillAdditionAction((ModifyCurrentSkillAdditionActionData)request.Data, request.Context));
            factory.Register(typeof(SetSourceValueSkillAdditionActionData), static request => new SetSourceValueSkillAdditionAction((SetSourceValueSkillAdditionActionData)request.Data, request.Context));
            factory.Register(typeof(ExecuteEffectsSkillAdditionActionData), static request => new ExecuteEffectsSkillAdditionAction((ExecuteEffectsSkillAdditionActionData)request.Data, request.Context));
            factory.Register(typeof(ReplayCurrentSkillAdditionActionData), static request => new ReplayCurrentSkillAdditionAction((ReplayCurrentSkillAdditionActionData)request.Data, request.Context));
            return factory;
        }
    }
}
