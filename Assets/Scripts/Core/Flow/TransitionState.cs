using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core {
    public class TransitionState : GameState
    {
        private TransitionData _transitionData;
        private TransitionUI _transitionUI;

        public override void OnEnter()
        {
            _transitionData = StateData as TransitionData;
            if (_transitionData == null)
            {
                Debug.LogError("[TransitionState] Invalid transition data");
                return;
            }

            OpenTransitionUI();
            if (!TransitionComponent.Instance.BeginLoadAndFadeOut(_transitionData))
            {
                Debug.LogError("[TransitionState] Failed to start transition load sequence.");
            }
        }

        public override void OnExit()
        {
            CloseTransitionUI();
            _transitionData = null;
        }

        private void OpenTransitionUI()
        {
            if (string.IsNullOrWhiteSpace(_transitionData?.TransitionUIName) || UIComponent.Instance == null)
                return;

            string transitionUIName = _transitionData.TransitionUIName;
            _transitionUI = UIComponent.Instance.Open(transitionUIName) as TransitionUI;
            if (_transitionUI == null)
            {
                Debug.LogError($"[TransitionState] Failed to open transition UI: {transitionUIName}");
                return;
            }

            UIComponent.Instance.SetLifetime(_transitionUI, UILifetime.Persistent);
        }

        private void CloseTransitionUI()
        {
            if (_transitionUI == null || UIComponent.Instance == null)
                return;

            UIComponent.Instance.ReleaseUI(_transitionUI);
            _transitionUI = null;
        }
    }
    public class TransitionData
    {
        public string TargetSceneName { get; set; }
        public System.Type TargetStateType { get; set; }
        public object TargetStateData { get; set; }
        public string TransitionUIName { get; set; }
        public IReadOnlyList<string> RequiredSceneNames { get; set; }
        public IReadOnlyList<string> RequiredSubSceneNames { get; set; }
        public bool ForceReloadTargetScene { get; set; }
        public System.Func<System.Collections.IEnumerator> PostLoadCoroutineFactory { get; set; }
        public System.Action OnComplete { get; set; }
    }
}
