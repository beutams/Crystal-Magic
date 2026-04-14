using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 队列式 UI 分组
    /// FIFO，队首可定时自动关闭
    /// </summary>
    public class QueueUIGroup : UIGroup
    {
        [SerializeField] private float _closeDuration = 3f;

        public override void ShowUI(UIBase panel)
        {
            SetupPanelOnAdd(panel);
            panel.EnqueueTime = Time.time;
            _panels.AddLast(panel);
            UIComponent.Instance?.OpenRootPanel(panel);
            RefreshSortingOrders();
        }

        public override void CloseUI(UIBase panel)
        {
            var node = FindNode(panel);
            if (node == null)
                return;

            _panels.Remove(node);
            UIComponent.Instance?.CloseRootPanel(panel);
            RefreshSortingOrders();
        }

        public override void Tick()
        {
            base.Tick();

            if (_panels.Count > 0)
            {
                UIBase head = _panels.First.Value;
                if (Time.time - head.EnqueueTime >= _closeDuration)
                {
                    CloseUI(head);
                }
            }
        }

        public UIBase Peek()
        {
            return _panels.Count > 0 ? _panels.First.Value : null;
        }
    }
}
