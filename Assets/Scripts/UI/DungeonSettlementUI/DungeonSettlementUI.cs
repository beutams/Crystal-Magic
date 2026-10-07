using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using UnityEngine;
using UnityEngine.UI;

public sealed class DungeonSettlementUI : UIBase<DungeonSettlementUIData, CrystalMagic.UI.DungeonSettlementUIModel>
{
    public event Action ConfirmClicked;
    public event Action<int> ItemClicked;
    private readonly List<DungeonSettlementUI_ItemView> _items = new();

    protected override void OnInit()
    {
        base.OnInit();
        UI.Panel_Items_Viewport_Content_Item.GameObject.SetActive(false);
    }

    public override void OnOpen()
    {
        base.OnOpen();
        UI.Panel_Confirm.ButtonPlus.onClick.AddListener(OnConfirmButtonClicked);
        UI.Panel_Items.GameObject.GetComponent<ScrollRect>().verticalNormalizedPosition = 1f;
    }

    public override void OnClose()
    {
        UI.Panel_Confirm.ButtonPlus.onClick.RemoveListener(OnConfirmButtonClicked);
        foreach (DungeonSettlementUI_ItemView item in _items)
            item.Clicked -= OnItemClicked;
        UISubViewBase.ReleaseAllToPool(_items);
        base.OnClose();
    }

    protected override void RefreshView()
    {
        if (Model == null)
            return;
        UI.Panel_Title.TextMeshProUGUI.text = Model.Title;
        UI.Panel_Summary.TextMeshProUGUI.text = Model.Summary;
        UI.Panel_Confirm_Label.TextMeshProUGUI.text = Model.ConfirmLabel;
        UI.Panel_Caption.TextMeshProUGUI.text = Model.Caption;
        UI.Panel_Empty.TextMeshProUGUI.text = Model.EmptyText;
        UI.Panel_Empty.GameObject.SetActive(Model.Items.Count == 0);
        UI.Panel_Detail.TextMeshProUGUI.text = Model.Items.Count > 0 ? Model.Detail : string.Empty;
        UI.Panel_Footnote.TextMeshProUGUI.text = Model.Footnote;
        UI.Panel_Money.TextMeshProUGUI.text = Model.Money > 0
            ? $"金币 {(Model.IsSuccess ? "+" : "−")}{Model.Money}" : string.Empty;
        Color resultColor = Model.IsSuccess ? Color.white : new Color(.72f, .64f, .62f);
        UI.Panel_BG_Header.Image.color = resultColor;
        UI.Panel_BG_Crest.Image.color = resultColor;
        UI.Panel_Title.TextMeshProUGUI.color = Model.IsSuccess ? new Color(1f, .93f, .78f) : new Color(.94f, .71f, .65f);
        UI.Panel_Confirm.ButtonPlus.enabled = !Model.IsReturning;
        UI.Panel_Confirm.Image.raycastTarget = !Model.IsReturning;

        while (_items.Count > Model.Items.Count)
        {
            int last = _items.Count - 1;
            _items[last].Clicked -= OnItemClicked;
            UISubViewBase.ReleaseToPool(_items[last]);
            _items.RemoveAt(last);
        }
        if (_items.Count < Model.Items.Count)
        {
            DungeonSettlementUI_ItemView template = UI.Panel_Items_Viewport_Content_Item.GameObject.GetComponent<DungeonSettlementUI_ItemView>();
            UISubViewBase.EnsurePoolCapacity(template, Model.Items.Count);
            while (_items.Count < Model.Items.Count)
            {
                DungeonSettlementUI_ItemView item = UISubViewBase.AcquireFromPool(template, UI.Panel_Items_Viewport_Content.RectTransform);
                if (item == null)
                    break;
                item.Clicked += OnItemClicked;
                _items.Add(item);
            }
        }
        for (int i = 0; i < _items.Count; i++)
            _items[i].Render(i, Model.Items[i], Model.IsSuccess, i == Model.SelectedIndex);
        LayoutRebuilder.ForceRebuildLayoutImmediate(UI.Panel_Items_Viewport_Content.RectTransform);
    }

    private void OnConfirmButtonClicked()
    {
        if (Model?.IsReturning != true)
            ConfirmClicked?.Invoke();
    }

    private void OnItemClicked(int index) => ItemClicked?.Invoke(index);
}
