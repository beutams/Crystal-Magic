using System.Collections;
using UnityEngine;

namespace CrystalMagic.Core {

    public class TransitionComponent : GameComponent<TransitionComponent>
    {
        private const string TransitionLockReason = "Transition";
        private TransitionData _activeTransitionData;
        private ITransitionUI _activeTransitionUI;
        private bool _isTransitioning;
        private bool _loadSequenceStarted;

        public override int Priority => 25;
        public bool IsTransitioning => _isTransitioning;

        public bool BeginFadeIn(TransitionData transitionData, ITransitionUI transitionUI)
        {
            if (_isTransitioning)
            {
                Debug.LogWarning("[TransitionComponent] Transition already in progress");
                return false;
            }

            if (transitionData == null)
            {
                Debug.LogError("[TransitionComponent] TransitionData is null");
                return false;
            }

            _activeTransitionData = transitionData;
            _activeTransitionUI = transitionUI;
            _loadSequenceStarted = false;
            _isTransitioning = true;

            GameGateComponent gate = GameGateComponent.Instance;
            gate.Lock(GameGateType.Simulation, TransitionLockReason);
            gate.Lock(GameGateType.PlayerInput, TransitionLockReason);
            gate.Lock(GameGateType.UIInput, TransitionLockReason);

            StartCoroutine(FadeInAsync(_activeTransitionUI, transitionData.TargetSceneName));
            return true;
        }

        public bool BeginLoadAndFadeOut(TransitionData transitionData)
        {
            if (!_isTransitioning || _activeTransitionData == null)
            {
                Debug.LogWarning("[TransitionComponent] Load sequence requested without an active transition.");
                return false;
            }

            if (_loadSequenceStarted)
            {
                Debug.LogWarning("[TransitionComponent] Load sequence already started.");
                return false;
            }

            if (transitionData == null || transitionData.TargetSceneName != _activeTransitionData.TargetSceneName)
            {
                Debug.LogWarning("[TransitionComponent] Load sequence requested with mismatched transition data.");
                return false;
            }

            _loadSequenceStarted = true;
            StartCoroutine(LoadAndFadeOutAsync(_activeTransitionData, _activeTransitionUI));
            return true;
        }

        private IEnumerator FadeInAsync(ITransitionUI transitionUI, string targetSceneName)
        {
            EventComponent.Instance?.Publish(new TransitionPhaseChangedEvent(TransitionPhase.FadeInStarted, targetSceneName));
            if (transitionUI != null)
            {
                yield return StartCoroutine(transitionUI.Show());
            }

            EventComponent.Instance?.Publish(new TransitionPhaseChangedEvent(TransitionPhase.FadeInCompleted, targetSceneName));
        }

        private IEnumerator LoadAndFadeOutAsync(TransitionData transitionData, ITransitionUI transitionUI)
        {
            EventComponent.Instance?.Publish(new TransitionPhaseChangedEvent(TransitionPhase.LoadStarted, transitionData.TargetSceneName));
            EventComponent.Instance?.Publish(new UISceneScopeChangedEvent(transitionData.TargetSceneName));
            PublishLoadProgress(transitionData.TargetSceneName, 0.05f, "Loading scene", transitionData.TargetSceneName);

            yield return StartCoroutine(LoadSceneAsync(transitionData));
            if (transitionData.PostLoadCoroutineFactory != null)
            {
                IEnumerator postLoadCoroutine = transitionData.PostLoadCoroutineFactory();
                if (postLoadCoroutine != null)
                    yield return StartCoroutine(postLoadCoroutine);
            }

            EventComponent.Instance?.Publish(new TransitionPhaseChangedEvent(TransitionPhase.LoadCompleted, transitionData.TargetSceneName, 1f));
            PublishLoadProgress(transitionData.TargetSceneName, 1f, "Load complete", transitionData.TargetSceneName);
            yield return StartCoroutine(FadeOutAsync(transitionUI, transitionData.TargetSceneName));

            GameGateComponent gate = GameGateComponent.Instance;
            gate?.Unlock(GameGateType.UIInput, TransitionLockReason);
            gate?.Unlock(GameGateType.PlayerInput, TransitionLockReason);
            gate?.Unlock(GameGateType.Simulation, TransitionLockReason);

            _activeTransitionData = null;
            _activeTransitionUI = null;
            _loadSequenceStarted = false;
            _isTransitioning = false;
        }

        private IEnumerator LoadSceneAsync(TransitionData transitionData)
        {
            bool forceReloadTargetScene = transitionData.ForceReloadTargetScene;
            yield return StartCoroutine(
                SceneComponent.Instance.LoadSceneAsyncCoroutine(
                    transitionData.TargetSceneName,
                    forceReload: forceReloadTargetScene,
                    onProgress: progress => PublishLoadProgress(
                        transitionData.TargetSceneName,
                        Mathf.Lerp(0.05f, 0.25f, progress),
                        "Loading scene",
                        transitionData.TargetSceneName))
            );

            if (transitionData.RequiredSubSceneNames == null)
                yield break;

            SceneComponent.Instance.SetSubScenesActive(transitionData.RequiredSubSceneNames);
            foreach (string subSceneName in transitionData.RequiredSubSceneNames)
            {
                PublishLoadProgress(transitionData.TargetSceneName, 0.27f, "Loading sub-scene", subSceneName);
                yield return StartCoroutine(SceneComponent.Instance.WaitForSubSceneLoadedCoroutine(subSceneName));
            }
        }

        private IEnumerator FadeOutAsync(ITransitionUI transitionUI, string targetSceneName)
        {
            EventComponent.Instance?.Publish(new TransitionPhaseChangedEvent(TransitionPhase.FadeOutStarted, targetSceneName));
            if (transitionUI != null)
            {
                yield return StartCoroutine(transitionUI.Hide());
            }

            EventComponent.Instance?.Publish(new TransitionPhaseChangedEvent(TransitionPhase.FadeOutCompleted, targetSceneName, 1f));
        }

        private static void PublishLoadProgress(string targetSceneName, float progress, string title, string detail)
        {
            EventComponent.Instance?.Publish(new TransitionLoadProgressChangedEvent(
                targetSceneName,
                Mathf.Clamp01(progress),
                title ?? string.Empty,
                detail ?? string.Empty));
        }
    }
}
