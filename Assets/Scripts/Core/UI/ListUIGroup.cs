using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 列表式 UI 分组
    /// 多个 UI 可同时显示
    /// </summary>
    public class ListUIGroup : UIGroup
    {
        public override void ShowUI(UIBase panel)
        {
            var node = FindNode(panel);
            if (node != null)
                return;

            SetupPanelOnAdd(panel);
            _panels.AddLast(panel);
            panel.gameObject.SetActive(true);
            panel.OnOpen();
            RefreshSortingOrders();
        }

        public override void CloseUI(UIBase panel)
        {
            var node = FindNode(panel);
            if (node == null)
                return;

            _panels.Remove(node);
            panel.OnClose();
            panel.gameObject.SetActive(false);
            RefreshSortingOrders();
        }
    }
}
