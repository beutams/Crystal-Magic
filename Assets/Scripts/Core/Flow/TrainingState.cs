using UnityEngine;

namespace CrystalMagic.Core
{
    public sealed class TrainingState : BattleStateBase
    {
        public const string SceneName = "TrainingScene";
        protected override string BattleSceneName => SceneName;

        protected override void OnEnterBattle()
        {
            Debug.Log("[TrainingState] Entered Training Ground");
            SaveDataComponent.Instance?.SetCurrentLocation(SaveAreaType.Training);

            if (StateData is LoadGameContext context)
            {
                Debug.Log($"[TrainingState] Loaded from save slot: {context.SaveIndex}");
            }
        }

        protected override void OnExitBattle()
        {
            Debug.Log("[TrainingState] Exited Training Ground");
        }

        public static TransitionData CreateEnterTransitionData(object data = null)
        {
            return new TransitionData
            {
                TargetSceneName = SceneName,
                TargetStateType = typeof(TrainingState),
                TargetStateData = data,
                TransitionUIName = "TransitionUI",
                ForceReloadTargetScene = true,
            };
        }
    }
}
