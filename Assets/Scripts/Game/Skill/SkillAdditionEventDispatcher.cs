using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;

namespace CrystalMagic.Game.Skill
{
    public static class SkillAdditionEventDispatcher
    {
        private static readonly ComparatorFactory s_comparatorFactory = CreateComparatorFactory();

        public static List<SkillAdditionAction> CreateActions(
            EntityManager entityManager,
            Entity entity,
            UnitSourceResolver sources,
            string eventName)
        {
            List<SkillAdditionAction> actions = new();
            if (sources == null || string.IsNullOrWhiteSpace(eventName))
                return actions;

            if (PlayerCurrentSkillUtility.TryGetCurrentAdditionId(entityManager, entity, out int selectedAdditionId))
                AppendActions(actions, entityManager, entity, sources, eventName, selectedAdditionId);

            return actions;
        }

        private static void AppendActions(
            List<SkillAdditionAction> destination,
            EntityManager entityManager,
            Entity entity,
            UnitSourceResolver sources,
            string eventName,
            int additionId)
        {
            if (additionId < 0)
                return;

            SkillAdditionData additionData = DataComponent.Instance?.Get<SkillAdditionData>(additionId);
            if (additionData?.Callbacks == null)
                return;

            for (int callbackIndex = 0; callbackIndex < additionData.Callbacks.Count; callbackIndex++)
            {
                SkillAdditionCallbackData callback = additionData.Callbacks[callbackIndex];
                if (callback == null ||
                    !string.Equals(callback.EventName, eventName, System.StringComparison.Ordinal) ||
                    !PassConditions(callback, sources) ||
                    callback.Actions == null)
                {
                    continue;
                }

                SkillAdditionActionContext context = new(entityManager, entity, sources, eventName, additionId);
                for (int actionIndex = 0; actionIndex < callback.Actions.Count; actionIndex++)
                {
                    SkillAdditionAction action = SkillAdditionActionRegistry.Create(callback.Actions[actionIndex], context);
                    if (action == null)
                        continue;

                    action.Start();
                    destination.Add(action);
                }
            }
        }

        private static bool PassConditions(SkillAdditionCallbackData callback, UnitSourceResolver sources)
        {
            return callback.Conditions == null ||
                   callback.Conditions.Count == 0 ||
                   s_comparatorFactory.BuildComparator(callback.Conditions, sources).GetResult(sources);
        }

        private static ComparatorFactory CreateComparatorFactory()
        {
            ComparatorFactory factory = new();
            ComparatorRegistry.RegisterAll(factory);
            return factory;
        }
    }
}
