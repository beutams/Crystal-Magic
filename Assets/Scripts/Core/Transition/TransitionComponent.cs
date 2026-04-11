using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 转场组件
    /// 职责：通过 UI 框架管理转场 UI
    /// </summary>
    public class TransitionComponent : GameComponent<TransitionComponent>
    {
        private ITransitionUI _transitionUI;
        private UIBase _transitionPanel;

        public override int Priority => 25;

        public override void Initialize()
        {
            base.Initialize();
            
            // 从对象池加载转场 UI 预制体
            LoadTransitionUI();
        }

        /// <summary>
        /// 加载转场 UI
        /// </summary>
        private void LoadTransitionUI()
        {
            GameObject uiInstance = PoolComponent.Instance.Get(AssetPathHelper.GetUIAsset("TransitionUI"));
            
            if (uiInstance != null)
            {
                _transitionPanel = uiInstance.GetComponent<UIBase>();
                if (_transitionPanel != null)
                {
                    _transitionUI = _transitionPanel.GetComponent<ITransitionUI>();
                    if (_transitionUI == null)
                    {
                        Debug.LogError("[TransitionComponent] TransitionUI prefab missing ITransitionUI component");
                    }
                }
                else
                {
                    Debug.LogError("[TransitionComponent] TransitionUI prefab missing UIBase component");
                }
            }
            else
            {
                Debug.LogError("[TransitionComponent] Failed to load TransitionUI prefab");
            }
        }

        /// <summary>
        /// 显示转场界面（协程版本）
        /// </summary>
        public System.Collections.IEnumerator ShowAsync()
        {
            if (_transitionUI != null)
            {
                // 先通过 UI 框架显示 UI
                if (_transitionPanel != null)
                {
                    UIComponent.Instance.ShowUI(_transitionPanel);
                }
                
                // 执行淡入效果
                yield return StartCoroutine(_transitionUI.Show());
            }
        }

        /// <summary>
        /// 隐藏转场界面（协程版本）
        /// </summary>
        public System.Collections.IEnumerator HideAsync()
        {
            if (_transitionUI != null)
            {
                // 执行淡出效果
                yield return StartCoroutine(_transitionUI.Hide());
                
                // 再通过 UI 框架关闭 UI
                if (_transitionPanel != null)
                {
                    UIComponent.Instance.CloseUI(_transitionPanel);
                }
            }
        }

        public override void Cleanup()
        {
            if (_transitionPanel != null)
            {
                UIComponent.Instance.CloseUI(_transitionPanel);
                PoolComponent.Instance.Release(_transitionPanel.gameObject);
            }
            base.Cleanup();
        }
    }
}
