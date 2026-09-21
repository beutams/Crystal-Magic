using UnityEngine;

namespace CrystalMagic.Core
{
    public sealed class TrainingState : BattleStateBase
    {
        public const string SceneName = "TrainingScene";
        public const string SubSceneName = "TrainingSubScene";
        protected override string BattleSceneName => SceneName;

        protected override void OnEnterBattle()
        {
            Debug.Log("[TrainingState] Entered Training Ground");

            LoadGameContext context = StateData as LoadGameContext;
            GameRuntimeStateUtility.BindPlayerCharacterData(context?.Character ?? new CharacterData());
            GameRuntimeStateUtility.ApplyPlayerRuntimeState(context?.Player);

            if (context != null)
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
                KeepCurrentMainScene = true,
                ActiveSubSceneNames = new[] { SubSceneName },
            };
        }
    }
}
