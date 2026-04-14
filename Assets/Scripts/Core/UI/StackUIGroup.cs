using UnityEngine;

namespace CrystalMagic.Core {
    /// <summary>
    /// 栈式 UI 分组
    /// 链表尾为栈顶，一次只交互最上层
    /// </summary>
    public class StackUIGroup : UIGroup
    {
        public override void ShowUI(UIBase panel)
        {
            var node = FindNode(panel);

            // 如果面板已存在且不是栈顶
            if (node != null && node != _panels.Last)
            {
                // 对旧栈顶调用 OnCovered
                UIBase oldTop = _panels.Last.Value;
                UIComponent.Instance?.CoverPanelTree(oldTop);

                // 移除旧节点
                _panels.Remove(node);

                // 再压栈
                _panels.AddLast(panel);
                UIComponent.Instance?.UncoverPanelTree(panel);
            }
            else if (node == null)
            {
                // 新面板
                if (_panels.Count > 0)
                {
                    UIBase oldTop = _panels.Last.Value;
                    UIComponent.Instance?.CoverPanelTree(oldTop);
                }

                SetupPanelOnAdd(panel);
                _panels.AddLast(panel);
                UIComponent.Instance?.OpenRootPanel(panel);
            }

            RefreshSortingOrders();
        }

        public override void CloseUI(UIBase panel)
        {
            var node = FindNode(panel);
            if (node == null)
                return;

            bool isTop = node == _panels.Last;

            UIComponent.Instance?.CloseRootPanel(panel);
            _panels.Remove(node);

            if (isTop && _panels.Count > 0)
            {
                UIBase newTop = _panels.Last.Value;
                UIComponent.Instance?.UncoverPanelTree(newTop);
            }

            RefreshSortingOrders();
        }

        public UIBase Peek()
        {
            return _panels.Count > 0 ? _panels.Last.Value : null;
        }

        public int Count => _panels.Count;
    }
}
