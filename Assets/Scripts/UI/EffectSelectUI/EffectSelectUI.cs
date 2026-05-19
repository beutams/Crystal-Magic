using CrystalMagic.Core;
using CrystalMagic.UI;
using System;
using System.Collections.Generic;
using UnityEngine;

public class EffectSelectUI : UIBase<EffectSelectUIData, EffectSelectUIModel>
{
    private readonly List<EffectSelectUI_ItemView> _itemViews = new();
    private Coroutine _itemHoverCoroutine;
    private EffectSelectAdditionDisplayData _hoveredItem;

    public event Action<EffectSelectAdditionDisplayData> ItemHoverReady;
    public event Action ItemHoverExited;
    public event Action<EffectSelectAdditionDisplayData> ItemSelected;

    protected override void OnInit()
    {
        base.OnInit();
    }

    public override void OnOpen()
    {
        base.OnOpen();
    }

    public override void OnClose()
    {
        CancelItemHover(true);
        UISubViewBase.ReleaseAllToPool(_itemViews);
        base.OnClose();
    }

    protected override void RefreshView()
    {
        if (Model == null)
            return;

        RenderItems(Model.Items);
    }

    private void RenderItems(IReadOnlyList<EffectSelectAdditionDisplayData> items)
    {
        int itemCount = items != null ? items.Count : 0;
        EnsureItemViews(itemCount);

        for (int i = 0; i < _itemViews.Count; i++)
        {
            EffectSelectAdditionDisplayData data = items != null && i < items.Count ? items[i] : null;
            _itemViews[i].Render(data);
        }
    }

    private void EnsureItemViews(int itemCount)
    {
        UI.ScrollView_Viewport_Content_Item.GameObject.SetActive(false);

        while (_itemViews.Count > itemCount)
        {
            int lastIndex = _itemViews.Count - 1;
            EffectSelectUI_ItemView itemView = _itemViews[lastIndex];
            UISubViewBase.ReleaseToPool(itemView);
            _itemViews.RemoveAt(lastIndex);
        }

        EffectSelectUI_ItemView templateView = UI.ScrollView_Viewport_Content_Item.GameObject.GetComponent<EffectSelectUI_ItemView>();
        if (templateView == null)
            return;

        UISubViewBase.EnsurePoolCapacity(templateView, itemCount, itemCount);

        while (_itemViews.Count < itemCount)
        {
            EffectSelectUI_ItemView itemView = UISubViewBase.AcquireFromPool(
                templateView,
                UI.ScrollView_Viewport_Content.GameObject.transform);
            if (itemView == null)
                break;

            BindItemView(itemView);
            _itemViews.Add(itemView);
        }
    }

    private void BindItemView(EffectSelectUI_ItemView itemView)
    {
        if (itemView == null)
            return;

        itemView.HoverEntered -= HandleItemHoverEntered;
        itemView.HoverExited -= HandleItemHoverExited;
        itemView.Clicked -= HandleItemClicked;
        itemView.HoverEntered += HandleItemHoverEntered;
        itemView.HoverExited += HandleItemHoverExited;
        itemView.Clicked += HandleItemClicked;
    }

    private void HandleItemHoverEntered(EffectSelectAdditionDisplayData data)
    {
        if (data == null)
            return;

        _hoveredItem = data;

        if (_itemHoverCoroutine != null)
        {
            StopCoroutine(_itemHoverCoroutine);
            _itemHoverCoroutine = null;
        }

        ItemHoverExited?.Invoke();
        _itemHoverCoroutine = StartCoroutine(ItemHoverDelayRoutine(data));
    }

    private void HandleItemHoverExited(EffectSelectAdditionDisplayData data)
    {
        if (data == null)
            return;

        if (!ReferenceEquals(_hoveredItem, data))
            return;

        CancelItemHover(true);
    }

    private void HandleItemClicked(EffectSelectAdditionDisplayData data)
    {
        if (data == null)
            return;

        CancelItemHover(true);
        ItemSelected?.Invoke(data);
    }

    private System.Collections.IEnumerator ItemHoverDelayRoutine(EffectSelectAdditionDisplayData data)
    {
        float delay = UIComponent.Instance != null ? UIComponent.Instance.GetHoverInfoDelaySeconds() : 2f;
        yield return new WaitForSeconds(delay);
        _itemHoverCoroutine = null;

        if (!ReferenceEquals(_hoveredItem, data))
            yield break;

        ItemHoverReady?.Invoke(data);
    }

    private void CancelItemHover(bool closeInfo)
    {
        _hoveredItem = null;

        if (_itemHoverCoroutine != null)
        {
            StopCoroutine(_itemHoverCoroutine);
            _itemHoverCoroutine = null;
        }

        if (closeInfo)
            ItemHoverExited?.Invoke();
    }
}
