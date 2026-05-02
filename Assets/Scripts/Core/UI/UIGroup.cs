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
        [SerializeField] protected Vector2 _referenceResolution = new(2560, 1440);
        [SerializeField] protected CanvasScaler.ScreenMatchMode _screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        [SerializeField] protected float _planeDistance = 1f;

        protected Canvas _canvas;
        protected CanvasScaler _canvasScaler;
        protected GraphicRaycaster _graphicRaycaster;

        public string GroupName => _groupName;
        public int BaseSortingOrder => _baseSortingOrder;
        internal IEnumerable<UIBase> Panels => _panels;

        public void ConfigureGroup(string groupName, int baseSortingOrder)
        {
            _groupName = groupName;
            _baseSortingOrder = baseSortingOrder;

            if (_canvas != null)
            {
                _canvas.sortingOrder = _baseSortingOrder;
            }
        }

        public void ConfigureCanvasSettings(Vector2 referenceResolution, CanvasScaler.ScreenMatchMode screenMatchMode, float planeDistance)
        {
            _referenceResolution = referenceResolution;
            _screenMatchMode = screenMatchMode;
            _planeDistance = planeDistance;
            ApplyCanvasSettings();
        }

        protected virtual void Awake()
        {
            // 获取或添加 Canvas
            _canvas = GetComponent<Canvas>();
            if (_canvas == null)
            {
                _canvas = gameObject.AddComponent<Canvas>();
            }

            ApplyCanvasSettings();

            // 设置 CanvasScaler
            _canvasScaler = GetComponent<CanvasScaler>();
            if (_canvasScaler == null)
            {
                _canvasScaler = gameObject.AddComponent<CanvasScaler>();
            }
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
            panel.transform.SetParent(transform, false);
            panel.transform.localScale = Vector3.one;
            panel.transform.localPosition = Vector3.zero;
            panel.transform.localRotation = Quaternion.identity;

            if (panel.transform is RectTransform rectTransform)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.anchoredPosition3D = Vector3.zero;
            }

            // 添加 Canvas
            Canvas panelCanvas = panel.GetComponent<Canvas>();
            if (panelCanvas == null)
            {
                panelCanvas = panel.gameObject.AddComponent<Canvas>();
            }
            panelCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            panelCanvas.overrideSorting = true;
            panelCanvas.worldCamera = CameraComponent.Instance.Current;
            panelCanvas.planeDistance = _planeDistance;

            // 添加 CanvasScaler
            CanvasScaler scaler = panel.GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.enabled = false;
            }

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

        internal bool RemovePanelSilently(UIBase panel)
        {
            LinkedListNode<UIBase> node = FindNode(panel);
            if (node == null)
                return false;

            _panels.Remove(node);
            return true;
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

        private void ApplyCanvasSettings()
        {
            if (_canvas == null || _canvasScaler == null)
                return;

            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = CameraComponent.Instance != null ? CameraComponent.Instance.Current : null;
            _canvas.sortingOrder = _baseSortingOrder;
            _canvas.planeDistance = _planeDistance;

            _canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _canvasScaler.referenceResolution = _referenceResolution;
            _canvasScaler.screenMatchMode = _screenMatchMode;
        }
    }
}
