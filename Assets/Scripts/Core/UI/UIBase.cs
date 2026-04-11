using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// UI 基础类
    /// 所有 UI 面板继承此类
    /// 若有对应 UIData，请继承 UIBase&lt;T&gt; 泛型版本
    /// </summary>
    public abstract class UIBase : MonoBehaviour
    {
        private Canvas _canvas;
        private bool _initialized = false;

        public Canvas Canvas
        {
            get
            {
                if (_canvas == null)
                    RefreshCanvas();
                return _canvas;
            }
        }

        public float EnqueueTime { get; set; }

        /// <summary>
        /// 刷新 Canvas 引用
        /// </summary>
        public void RefreshCanvas()
        {
            _canvas = GetComponent<Canvas>();
        }

        /// <summary>
        /// 首次初始化（仅一次）
        /// </summary>
        public void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;
            OnInit();
        }

        /// <summary>
        /// 首次初始化时调用（仅一次）
        /// </summary>
        protected virtual void OnInit()
        {
        }

        /// <summary>
        /// UI 打开时调用
        /// </summary>
        public virtual void OnOpen()
        {
        }

        /// <summary>
        /// UI 关闭时调用
        /// </summary>
        public virtual void OnClose()
        {
        }

        /// <summary>
        /// UI 被盖住时调用（栈式分组）
        /// </summary>
        public virtual void OnCovered()
        {
        }

        /// <summary>
        /// UI 被揭开时调用（栈式分组）
        /// </summary>
        public virtual void OnUncovered()
        {
        }

        /// <summary>
        /// 每帧更新
        /// </summary>
        public virtual void OnUpdate()
        {
        }

        /// <summary>
        /// 关闭自己
        /// </summary>
        public void Close()
        {
            if (UIComponent.Instance != null)
            {
                UIComponent.Instance.CloseUI(this);
            }
        }
    }

    /// <summary>
    /// 带 UIData 的 UI 基类
    /// T 为对应的自动生成的 UIData 子类
    /// OnInit 时自动将 T 的字段绑定到当前 GameObject 的子物体
    /// </summary>
    public abstract class UIBase<T> : UIBase where T : UIData, new()
    {
        /// <summary>
        /// 子物体引用，OnInit 完成后可安全使用
        /// </summary>
        protected T UI { get; private set; }

        protected override void OnInit()
        {
            UI = new T();
            UI.Bind(transform);
        }
    }
}
