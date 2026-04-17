using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core {
    public class TransitionState : GameState
    {
        private string _targetSceneName;
        private System.Type _targetStateType;
        private System.Action _onTransitionComplete;
        private bool _isTransitioning = false;

        public override void OnEnter()
        {
            if (StateData is TransitionData transData)
            {
                _targetSceneName = transData.TargetSceneName;
                _targetStateType = transData.TargetStateType;
                _onTransitionComplete = transData.OnComplete;

                Debug.Log($"[TransitionState] Starting transition to {_targetSceneName}");
                GameFlowComponent.Instance.StartCoroutine(DoTransition());
            }
            else
            {
                Debug.LogError("[TransitionState] Invalid transition data!");
            }
        }

        public override void OnExit()
        {
            Debug.Log("[TransitionState] Transition completed");
            _isTransitioning = false;
        }

        public override void OnUpdate()
        {
        }

        private System.Collections.IEnumerator DoTransition()
        {
            _isTransitioning = true;
            TransitionComponent transition = TransitionComponent.Instance;
            SceneComponent scene = SceneComponent.Instance;

            Debug.Log("[TransitionState] Showing transition UI");
            yield return GameFlowComponent.Instance.StartCoroutine(transition.ShowAsync());

            Debug.Log($"[TransitionState] Loading scene: {_targetSceneName}");
            yield return GameFlowComponent.Instance.StartCoroutine(
                scene.LoadSceneAsyncCoroutine(_targetSceneName)
            );

            if (StateData is TransitionData transitionData && transitionData.RequiredSubSceneNames != null)
            {
                foreach (string subSceneName in transitionData.RequiredSubSceneNames)
                {
                    yield return GameFlowComponent.Instance.StartCoroutine(
                        scene.WaitForSubSceneLoadedCoroutine(subSceneName)
                    );
                }
            }

            Debug.Log("[TransitionState] Hiding transition UI");
            yield return GameFlowComponent.Instance.StartCoroutine(transition.HideAsync());

            Debug.Log("[TransitionState] Transitioning to target state");

            object targetStateData = null;
            if (StateData is TransitionData transData)
            {
                targetStateData = transData.TargetStateData;
            }

            GameFlowComponent.Instance.SetState(_targetStateType, targetStateData);
            _onTransitionComplete?.Invoke();
        }

        public bool IsTransitioning => _isTransitioning;
    }
    public class TransitionData
    {
        public string TargetSceneName { get; set; }
        public System.Type TargetStateType { get; set; }
        public object TargetStateData { get; set; }
        public IReadOnlyList<string> RequiredSubSceneNames { get; set; }
        public System.Action OnComplete { get; set; }
    }
}
