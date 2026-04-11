using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 转场状态
    /// 在两个主要状态之间进行转场，处理场景加载
    /// 通过 TransitionData 传入目标场景、目标状态类型、目标状态数据及完成回调
    /// </summary>
    public class TransitionState : GameState
    {
        private string _targetSceneName;
        private System.Type _targetStateType;
        private System.Action _onTransitionComplete;
        private bool _isTransitioning = false;

        public override void OnEnter()
        {
            // 从 StateData 中获取转场参数
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

            // 1. 显示转场UI
            Debug.Log("[TransitionState] Showing transition UI");
            yield return GameFlowComponent.Instance.StartCoroutine(transition.ShowAsync());

            // 2. 后台加载场景
            Debug.Log($"[TransitionState] Loading scene: {_targetSceneName}");
            yield return GameFlowComponent.Instance.StartCoroutine(
                scene.LoadSceneAsyncCoroutine(_targetSceneName)
            );

            // 3. 隐藏转场UI
            Debug.Log("[TransitionState] Hiding transition UI");
            yield return GameFlowComponent.Instance.StartCoroutine(transition.HideAsync());

            // 4. 转移到目标状态（传递数据）
            Debug.Log($"[TransitionState] Transitioning to target state");
            
            // 获取转场数据中携带的目标状态数据
            object targetStateData = null;
            if (StateData is TransitionData transData)
            {
                targetStateData = transData.TargetStateData;
            }
            
            GameFlowComponent.Instance.SetState(_targetStateType, targetStateData);

            // 5. 转场完成后调用委托
            _onTransitionComplete?.Invoke();
        }

        public bool IsTransitioning => _isTransitioning;
    }

    /// <summary>
    /// 转场数据结构
    /// 用于传递转场所需的所有信息
    /// </summary>
    public class TransitionData
    {
        public string TargetSceneName { get; set; }
        public System.Type TargetStateType { get; set; }
        public object TargetStateData { get; set; }
        public System.Action OnComplete { get; set; }
    }
}
