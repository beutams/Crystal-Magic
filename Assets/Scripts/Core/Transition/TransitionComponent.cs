using System.Collections;
using System.Collections.Generic;
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
            DungeonFlowTiming.BeginStage(3, "Transition UI 淡入", targetSceneName);
            EventComponent.Instance?.Publish(new TransitionPhaseChangedEvent(TransitionPhase.FadeInStarted, targetSceneName));
            if (transitionUI != null)
            {
                yield return StartCoroutine(transitionUI.Show());
            }

            DungeonFlowTiming.EndStage(3, "FadeIn 已完成");
            EventComponent.Instance?.Publish(new TransitionPhaseChangedEvent(TransitionPhase.FadeInCompleted, targetSceneName));
        }

        private IEnumerator LoadAndFadeOutAsync(TransitionData transitionData, ITransitionUI transitionUI)
        {
            EventComponent.Instance?.Publish(new TransitionPhaseChangedEvent(TransitionPhase.LoadStarted, transitionData.TargetSceneName));
            EventComponent.Instance?.Publish(new UISceneScopeChangedEvent(transitionData.TargetSceneName));
            PublishLoadProgress(transitionData.TargetSceneName, 0.05f, "Loading scene", transitionData.TargetSceneName);

            if (transitionData.PreLoadCoroutineFactory != null)
            {
                IEnumerator preLoadCoroutine = transitionData.PreLoadCoroutineFactory();
                if (preLoadCoroutine != null)
                    yield return StartCoroutine(preLoadCoroutine);
            }

            if (transitionData.LoadError == null)
                yield return StartCoroutine(LoadSceneAsync(transitionData));
            if (transitionData.LoadError == null && transitionData.PostLoadCoroutineFactory != null)
            {
                IEnumerator postLoadCoroutine = transitionData.PostLoadCoroutineFactory();
                if (postLoadCoroutine != null)
                    yield return StartCoroutine(postLoadCoroutine);
            }

            if (transitionData.LoadError != null)
            {
                Debug.LogError(transitionData.LoadError);
                // 失败不能进入原来的目标状态，更不能执行成功后的保存/清理回调。
                transitionData.OnComplete = null;
                if (transitionData.OnLoadFailed != null)
                    transitionData.OnLoadFailed(transitionData.LoadError);
                else
                {
                    transitionData.TargetStateType = typeof(MainMenuState);
                    transitionData.TargetStateData = null;
                }
            }

            DungeonFlowTiming.BeginStage(17, "淡出转场、进入 DungeonState 并解锁输入");
            if (transitionData.LoadError == null)
            {
                EventComponent.Instance?.Publish(new TransitionPhaseChangedEvent(TransitionPhase.LoadCompleted, transitionData.TargetSceneName, 1f));
                PublishLoadProgress(transitionData.TargetSceneName, 1f, "Load complete", transitionData.TargetSceneName);
            }
            else
                PublishLoadProgress(transitionData.TargetSceneName, 1f, "Load failed", transitionData.LoadError);
            yield return StartCoroutine(FadeOutAsync(transitionUI, transitionData.TargetSceneName));

            GameGateComponent gate = GameGateComponent.Instance;
            gate?.Unlock(GameGateType.UIInput, TransitionLockReason);
            gate?.Unlock(GameGateType.PlayerInput, TransitionLockReason);
            gate?.Unlock(GameGateType.Simulation, TransitionLockReason);

            _activeTransitionData = null;
            _activeTransitionUI = null;
            _loadSequenceStarted = false;
            _isTransitioning = false;
            DungeonFlowTiming.EndStage(17, "DungeonState 已进入，输入与模拟已解锁");
            DungeonFlowTiming.Complete("角色现在可以接收移动输入");
        }

        private IEnumerator LoadSceneAsync(TransitionData transitionData)
        {
            DungeonFlowTiming.BeginStage(5, "加载目标场景", transitionData.TargetSceneName);
            if (transitionData.KeepCurrentMainScene)
            {
                GameWorldManager.PrepareForSceneLoad(transitionData.TargetSceneName);
                PublishLoadProgress(transitionData.TargetSceneName, 0.25f, "Keeping main scene", transitionData.TargetSceneName);
            }
            else
            {
                yield return StartCoroutine(
                    SceneComponent.Instance.LoadSceneAsyncCoroutine(
                        transitionData.TargetSceneName,
                        forceReload: transitionData.ForceReloadTargetScene,
                        onProgress: progress => PublishLoadProgress(
                            transitionData.TargetSceneName,
                            Mathf.Lerp(0.05f, 0.25f, progress),
                            "Loading scene",
                            transitionData.TargetSceneName))
                );
            }
            DungeonFlowTiming.EndStage(5, "目标场景已就绪");

            IReadOnlyList<string> activeSubSceneNames = transitionData.ActiveSubSceneNames ?? transitionData.RequiredSubSceneNames;
            DungeonFlowTiming.BeginStage(6, "激活并等待目标 SubScene");
            if (activeSubSceneNames == null)
            {
                DungeonFlowTiming.EndStage(6, "没有 SubScene 变更");
                yield break;
            }

            SceneComponent.Instance.SetSubScenesActive(activeSubSceneNames);
            foreach (string subSceneName in activeSubSceneNames)
            {
                PublishLoadProgress(transitionData.TargetSceneName, 0.27f, "Loading sub-scene", subSceneName);
                yield return StartCoroutine(SceneComponent.Instance.WaitForSubSceneLoadedCoroutine(subSceneName));
                if (!SceneComponent.Instance.IsSubSceneLoaded(subSceneName))
                {
                    transitionData.LoadError = $"加载子场景超时：{subSceneName}。";
                    yield break;
                }
            }
            DungeonFlowTiming.EndStage(6, "目标 SubScene 已完成");
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
