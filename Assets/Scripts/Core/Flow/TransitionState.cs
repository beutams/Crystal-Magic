using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core {
    public class TransitionState : GameState
    {
        private TransitionData _transitionData;
        private UIBase _transitionPanel;
        private ITransitionLoadingUI _transitionUI;
        private bool _eventsBound;

        public override void OnEnter()
        {
            _transitionData = StateData as TransitionData;
            if (_transitionData == null)
            {
                Debug.LogError("[TransitionState] Invalid transition data");
                return;
            }

            OpenTransitionUI();
            BindEvents();
            if (TransitionComponent.Instance == null ||
                !TransitionComponent.Instance.BeginLoadAndFadeOut(_transitionData))
            {
                Debug.LogError("[TransitionState] Failed to start transition load sequence.");
            }
        }

        public override void OnExit()
        {
            UnbindEvents();
            CloseTransitionUI();
            _transitionData = null;
        }

        private void OpenTransitionUI()
        {
            if (string.IsNullOrWhiteSpace(_transitionData?.TransitionUIName) || UIComponent.Instance == null)
                return;

            string transitionUIName = _transitionData.TransitionUIName;
            _transitionPanel = UIComponent.Instance.Open(transitionUIName);
            if (_transitionPanel == null)
            {
                Debug.LogError($"[TransitionState] Failed to open transition UI: {transitionUIName}");
                return;
            }

            UIComponent.Instance.SetLifetime(_transitionPanel, UILifetime.Persistent);
            _transitionUI = _transitionPanel.GetComponent<ITransitionLoadingUI>();
            if (_transitionUI == null)
            {
                Debug.LogWarning($"[TransitionState] Transition UI '{transitionUIName}' missing ITransitionLoadingUI.");
                return;
            }

            _transitionUI.BindTransitionData(_transitionData);
        }

        private void CloseTransitionUI()
        {
            if (_transitionPanel == null || UIComponent.Instance == null)
                return;

            UIComponent.Instance.ReleaseUI(_transitionPanel);
            _transitionPanel = null;
            _transitionUI = null;
        }

        private void BindEvents()
        {
            if (_eventsBound || EventComponent.Instance == null)
                return;

            EventComponent.Instance.Subscribe<TransitionPhaseChangedEvent>(HandleTransitionPhaseChanged);
            _eventsBound = true;
        }

        private void UnbindEvents()
        {
            if (!_eventsBound || EventComponent.Instance == null)
                return;

            EventComponent.Instance.Unsubscribe<TransitionPhaseChangedEvent>(HandleTransitionPhaseChanged);
            _eventsBound = false;
        }

        private void HandleTransitionPhaseChanged(TransitionPhaseChangedEvent gameEvent)
        {
            if (_transitionData == null || gameEvent.TargetSceneName != _transitionData.TargetSceneName)
                return;

            _transitionUI?.RefreshTransitionPhase(gameEvent.Phase, gameEvent.Progress);
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
        public System.Action OnComplete { get; set; }
    }
}
