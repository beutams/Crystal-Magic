using UnityEngine;
using CrystalMagic.UI;

namespace CrystalMagic.Core
{
    public sealed class TrainingState : BattleStateBase
    {
        public const string SceneName = "TrainingScene";
        protected override string BattleSceneName => SceneName;
        private TrainingDummyStatsUI _trainingDummyStatsUI;
        private TrainingDebugUI _trainingDebugUI;

        protected override void OnEnterBattle()
        {
            Debug.Log("[TrainingState] Entered Training Ground");
            SaveDataComponent.Instance?.SetCurrentLocation(SaveAreaType.Training);

            _trainingDummyStatsUI = UIComponent.Instance.Open<TrainingDummyStatsUI>();
            if (_trainingDummyStatsUI != null)
                UIComponent.Instance.SetLifetime(_trainingDummyStatsUI, UILifetime.Manual);

            _trainingDebugUI = UIComponent.Instance.Open<TrainingDebugUI>();
            if (_trainingDebugUI != null)
                UIComponent.Instance.SetLifetime(_trainingDebugUI, UILifetime.Manual);

            if (StateData is LoadGameContext context)
            {
                Debug.Log($"[TrainingState] Loaded from save slot: {context.SaveIndex}");
            }
        }

        protected override void OnExitBattle()
        {
            if (_trainingDummyStatsUI != null)
            {
                UIComponent.Instance.ReleaseUI(_trainingDummyStatsUI);
                _trainingDummyStatsUI = null;
            }

            if (_trainingDebugUI != null)
            {
                UIComponent.Instance.ReleaseUI(_trainingDebugUI);
                _trainingDebugUI = null;
            }

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
