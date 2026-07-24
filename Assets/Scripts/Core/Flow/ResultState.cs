using UnityEngine;

namespace CrystalMagic.Core
{
    public enum ResultOutcome
    {
        Success,
        Failure,
    }

    public sealed class ResultStateData
    {
        public ResultOutcome Outcome { get; set; }
        public LoadGameContext NextContext { get; set; }

        public static ResultStateData Create(ResultOutcome outcome, LoadGameContext nextContext)
        {
            return new ResultStateData
            {
                Outcome = outcome,
                NextContext = nextContext,
            };
        }
    }

    public sealed class ResultState : GameState
    {
        public override void OnEnter()
        {
            ResultStateData data = StateData as ResultStateData;
            ResultOutcome outcome = data?.Outcome ?? ResultOutcome.Success;
            LoadGameContext nextContext = data?.NextContext ?? SaveDataComponent.Instance.CreateLoadGameContext(SaveAreaType.Town);

            Debug.Log($"[ResultState] Entered Result. Outcome={outcome}");
            GameFlowComponent.Instance.BeginTransition(TownState.CreateEnterTransitionData(nextContext));
        }

        public override void OnExit()
        {
            Debug.Log("[ResultState] Exited Result");
        }
    }
}
