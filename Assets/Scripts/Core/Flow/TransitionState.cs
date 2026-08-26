using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Core {
    public class TransitionState : GameState
    {
        private TransitionData _transitionData;

        public override void OnEnter()
        {
            _transitionData = StateData as TransitionData;
            if (_transitionData == null)
            {
                Debug.LogError("[TransitionState] Invalid transition data");
                return;
            }

            if (!TransitionComponent.Instance.BeginLoadAndFadeOut(_transitionData))
            {
                Debug.LogError("[TransitionState] Failed to start transition load sequence.");
            }
        }

        public override void OnExit()
        {
            _transitionData = null;
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
