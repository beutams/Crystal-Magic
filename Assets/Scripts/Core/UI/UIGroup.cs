using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CrystalMagic.Core {
    /// <summary>
    /// UI 分组抽象基类
    /// </summary>
    public abstract class UIGroup : MonoBehaviour
    {
        protected LinkedList<UIBase> _panels = new LinkedList<UIBase>();

        [SerializeField] protected string _groupName;
        [SerializeField] protected int _baseSortingOrder = 0;

        protected Canvas _canvas;
        protected CanvasScaler _canvasScaler;
        protected GraphicRaycaster _graphicRaycaster;

        public string GroupName => _groupName;
        public int BaseSortingOrder => _baseSortingOrder;
        internal IEnumerable<UIBase> Panels => _panels;

        protected virtual void Awake()
        {
            // 获取或添加 Canvas
            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = gameObject.AddComponent<Canvas>();
            }

            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.sortingOrder = _baseSortingOrder;

            // 设置 CanvasScaler
            _canvasScaler = GetComponent<CanvasScaler>();
            if (_canvasScaler == null)
            {
                _canvasScaler = gameObject.AddComponent<CanvasScaler>();
            }
            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            // 设置 GraphicRaycaster
            _graphicRaycaster = GetComponent<GraphicRaycaster>();
            if (_graphicRaycaster == null)
            {
                _graphicRaycaster = gameObject.AddComponent<GraphicRaycaster>();
            }

            // 注册到 UIComponent
            if (!string.IsNullOrEmpty(_groupName))
            {
                UIComponent.Instance?.RegisterGroup(_groupName, this);
            }
        }

        /// <summary>
        /// 显示 UI
        /// </summary>
        public abstract void ShowUI(UIBase panel);

        /// <summary>
        /// 关闭 UI
        /// </summary>
        public abstract void CloseUI(UIBase panel);

        /// <summary>
        /// 设置面板到组内
        /// </summary>
        protected void SetupPanelOnAdd(UIBase panel)
        {
            panel.transform.SetParent(transform);

            // 添加 Canvas
            Canvas panelCanvas = panel.GetComponent<Canvas>();
            if (panelCanvas == null)
            {
                panelCanvas = panel.gameObject.AddComponent<Canvas>();
            }
            panelCanvas.overrideSorting = true;

            // 添加 CanvasScaler
            CanvasScaler scaler = panel.GetComponent<CanvasScaler>();
            if (scaler == null)
            {
                scaler = panel.gameObject.AddComponent<CanvasScaler>();
            }
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

            // 添加 GraphicRaycaster
            GraphicRaycaster raycaster = panel.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = panel.gameObject.AddComponent<GraphicRaycaster>();
            }

            panel.RefreshCanvas();
            panel.EnsureInitialized();
        }

        internal void AttachPanel(UIBase panel)
        {
            SetupPanelOnAdd(panel);
        }

        /// <summary>
        /// 刷新排序
        /// </summary>
        protected void RefreshSortingOrders()
        {
            if (UIComponent.Instance != null)
            {
                UIComponent.Instance.RefreshGroupSortingOrders(this);
                return;
            }

            RefreshRootSortingOrders();
        }

        internal void RefreshRootSortingOrders()
        {
            int order = _baseSortingOrder;
            foreach (var panel in _panels)
            {
                panel.Canvas.sortingOrder = order;
                order += 100;
            }
        }

        /// <summary>
        /// 查找面板节点
        /// </summary>
        protected LinkedListNode<UIBase> FindNode(UIBase panel)
        {
            for (var node = _panels.First; node != null; node = node.Next)
            {
                if (node.Value == panel)
                    return node;
            }
            return null;
        }

        /// <summary>
        /// 每帧更新
        /// </summary>
        public virtual void Tick()
        {
            foreach (var panel in _panels)
            {
                panel.OnUpdate();
            }
        }
    }
}
