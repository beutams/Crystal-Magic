using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core
{
    public class GameFlowComponent : GameComponent<GameFlowComponent>
    {
        #region State Flow Fields
        private GameState _currentState;
        private readonly Dictionary<Type, GameState> _stateCache = new();
        #endregion

        #region Transition Flow Fields
        private TransitionData _activeTransitionData;
        private UIBase _activeTransitionPanel;
        private ITransitionUI _activeTransitionUI;
        private bool _isTransitioning;
        #endregion

        public override int Priority => 30;

        public override void Initialize()
        {
            base.Initialize();
            BindEvents();
        }

        private void Update()
        {
            if (_isTransitioning)
                return;

            _currentState?.OnUpdate();
        }

        public void SetState<T>(object data = null) where T : GameState, new()
        {
            if (_isTransitioning)
                return;

            GameState newState = GetOrCreateState<T>();
            SetStateInternal(newState, data);
        }

        public void SetState(Type stateType, object data = null)
        {
            if (_isTransitioning || stateType == null)
                return;

            SetStateInternal(GetOrCreateState(stateType), data);
        }

        public void BeginTransition(TransitionData transitionData)
        {
            if (_isTransitioning)
            {
                Debug.LogWarning("[GameFlow] Transition request ignored because a transition is already in progress.");
                return;
            }

            if (transitionData == null)
            {
                Debug.LogError("[GameFlow] TransitionData is null.");
                return;
            }

            _activeTransitionData = transitionData;
            _isTransitioning = true;
            OpenTransitionUI(transitionData);

            if (!TransitionComponent.Instance.BeginFadeIn(transitionData, _activeTransitionUI))
            {
                Debug.LogError("[GameFlow] Failed to start transition fade-in.");
                ReleaseTransitionUI();
                _activeTransitionData = null;
                _isTransitioning = false;
            }
        }

        public bool IsInState<T>() where T : GameState
        {
            return _currentState is T;
        }

        public GameState GetCurrentState()
        {
            return _currentState;
        }

        protected virtual void OnStateChanged(GameState oldState, GameState newState)
        {
            string oldName = oldState?.GetType().Name ?? "None";
            string newName = newState?.GetType().Name ?? "None";
            Debug.Log($"[GameFlow] State changed: {oldName} to {newName}");
        }

        public override void Cleanup()
        {
            UnbindEvents();
            ReleaseTransitionUI();

            _currentState?.OnExit();
            _currentState = null;
            _activeTransitionData = null;
            _isTransitioning = false;

            foreach (GameState state in _stateCache.Values)
            {
                state?.OnExit();
            }

            _stateCache.Clear();
            base.Cleanup();
        }

        #region State Flow
        private void SetStateInternal(GameState newState, object data = null)
        {
            if (newState == null || _currentState == newState)
                return;

            GameState oldState = _currentState;
            _currentState?.OnExit();

            _currentState = newState;
            _currentState.SetData(data);
            _currentState.OnEnter();

            OnStateChanged(oldState, newState);
        }

        private GameState GetOrCreateState(Type stateType)
        {
            if (stateType == null)
                return null;

            if (!_stateCache.TryGetValue(stateType, out GameState state) || state == null)
            {
                state = Activator.CreateInstance(stateType) as GameState;
                _stateCache[stateType] = state;
            }

            return state;
        }

        private T GetOrCreateState<T>() where T : GameState, new()
        {
            Type stateType = typeof(T);
            if (!_stateCache.ContainsKey(stateType))
            {
                _stateCache[stateType] = new T();
            }

            return (T)_stateCache[stateType];
        }
        #endregion

        #region Transition Flow
        private void BindEvents()
        {
            if (EventComponent.Instance == null)
                return;

            EventComponent.Instance.Subscribe<TransitionPhaseChangedEvent>(HandleTransitionPhaseChanged);
        }

        private void UnbindEvents()
        {
            if (EventComponent.Instance == null)
                return;

            EventComponent.Instance.Unsubscribe<TransitionPhaseChangedEvent>(HandleTransitionPhaseChanged);
        }

        private void HandleTransitionPhaseChanged(TransitionPhaseChangedEvent gameEvent)
        {
            if (_activeTransitionData == null || gameEvent.TargetSceneName != _activeTransitionData.TargetSceneName)
                return;

            switch (gameEvent.Phase)
            {
                case TransitionPhase.FadeInCompleted:
                    EnterTransitionState();
                    break;
                case TransitionPhase.FadeOutStarted:
                    EnterTargetState();
                    break;
                case TransitionPhase.FadeOutCompleted:
                    CompleteTransition();
                    break;
            }
        }

        private void EnterTransitionState()
        {
            if (_currentState is TransitionState)
                return;

            GameState transitionState = GetOrCreateState(typeof(TransitionState));
            if (transitionState == null)
            {
                Debug.LogError("[GameFlow] Failed to create TransitionState.");
                return;
            }

            SetStateInternal(transitionState, _activeTransitionData);
        }

        private void EnterTargetState()
        {
            if (_activeTransitionData?.TargetStateType == null)
                return;

            GameState targetState = GetOrCreateState(_activeTransitionData.TargetStateType);
            if (targetState == null || _currentState == targetState)
                return;

            SetStateInternal(targetState, _activeTransitionData.TargetStateData);
        }

        private void CompleteTransition()
        {
            System.Action onComplete = _activeTransitionData?.OnComplete;
            ReleaseTransitionUI();
            _activeTransitionData = null;
            _isTransitioning = false;
            onComplete?.Invoke();
        }

        private void OpenTransitionUI(TransitionData transitionData)
        {
            ReleaseTransitionUI();

            if (UIComponent.Instance == null || string.IsNullOrWhiteSpace(transitionData?.TransitionUIName))
                return;

            string transitionUIName = transitionData.TransitionUIName;
            _activeTransitionPanel = UIComponent.Instance.Open(transitionUIName);
            if (_activeTransitionPanel == null)
            {
                Debug.LogError($"[GameFlow] Failed to open transition UI: {transitionUIName}");
                return;
            }

            UIComponent.Instance.SetLifetime(_activeTransitionPanel, UILifetime.Persistent);
            _activeTransitionUI = _activeTransitionPanel.GetComponent<ITransitionUI>();
            if (_activeTransitionUI == null)
            {
                Debug.LogError($"[GameFlow] Transition UI '{transitionUIName}' missing ITransitionUI.");
                ReleaseTransitionUI();
            }
        }

        private void ReleaseTransitionUI()
        {
            if (_activeTransitionPanel != null && UIComponent.Instance != null)
            {
                UIComponent.Instance.ReleaseUI(_activeTransitionPanel);
            }

            _activeTransitionPanel = null;
            _activeTransitionUI = null;
        }
        #endregion
    }
}
