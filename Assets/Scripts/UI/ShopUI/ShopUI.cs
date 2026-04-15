using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.UI;
using UnityEngine;

public class ShopUI : UIBase<ShopUIData>
{
    private readonly List<ShopUI_CommodityItemView> _commodityItemViews = new();
    private readonly List<ShopUI_InventoryItemView> _inventoryItemViews = new();

    private ShopUIModel _model;
    private bool _isOpened;
    private bool _isModelEventSubscribed;

    public void BindModel(ShopUIModel model)
    {
        if (_model == model)
            return;

        if (_model != null && _isOpened)
            UnsubscribeModelEvents();

        _model = model;

        if (_model != null && _isOpened)
        {
            SubscribeModelEvents();
            Render();
        }
    }

    public override void OnOpen()
    {
        _isOpened = true;
        SubscribeModelEvents();

        if (_model != null)
            Render();
    }

    public override void OnClose()
    {
        UnsubscribeModelEvents();
        _isOpened = false;
    }

    private void Render()
    {
        RenderCommodities(_model.Commodities);
        RenderInventory(_model.InventoryItems, _model.InventorySlotCount);
    }

    private void RenderCommodities(IReadOnlyList<ShopCommodityDisplayData> commodities)
    {
        int commodityCount = commodities != null ? commodities.Count : 0;
        EnsureCommodityItemViews(commodityCount);

        for (int i = 0; i < _commodityItemViews.Count; i++)
        {
            ShopCommodityDisplayData data = commodities != null && i < commodities.Count ? commodities[i] : null;
            _commodityItemViews[i].Render(data);
        }
    }

    private void RenderInventory(ShopInventoryDisplayData[] inventoryItems, int slotCount)
    {
        EnsureInventoryItemViews(slotCount);

        for (int i = 0; i < _inventoryItemViews.Count; i++)
        {
            ShopInventoryDisplayData data = inventoryItems != null && i < inventoryItems.Length ? inventoryItems[i] : null;
            _inventoryItemViews[i].Render(data);
        }
    }

    private void EnsureCommodityItemViews(int itemCount)
    {
        _commodityItemViews.Clear();

        for (int i = 0; i < UI.ShopView_Viewport_Content.GameObject.transform.childCount; i++)
        {
            ShopUI_CommodityItemView itemView = UI.ShopView_Viewport_Content.GameObject.transform.GetChild(i).GetComponent<ShopUI_CommodityItemView>();
            itemView.Rebind();

            if (_commodityItemViews.Count < itemCount)
            {
                itemView.gameObject.SetActive(true);
                _commodityItemViews.Add(itemView);
            }
            else
            {
                itemView.gameObject.SetActive(false);
            }
        }

        while (_commodityItemViews.Count < itemCount)
        {
            GameObject clone = Instantiate(UI.ShopView_Viewport_Content_CommodityItem.GameObject, UI.ShopView_Viewport_Content.GameObject.transform);
            clone.name = UI.ShopView_Viewport_Content_CommodityItem.GameObject.name;
            clone.SetActive(true);

            ShopUI_CommodityItemView itemView = clone.GetComponent<ShopUI_CommodityItemView>();
            itemView.Rebind();
            _commodityItemViews.Add(itemView);
        }
    }

    private void EnsureInventoryItemViews(int itemCount)
    {
        _inventoryItemViews.Clear();

        for (int i = 0; i < UI.InventoryView_Viewport_Content.GameObject.transform.childCount; i++)
        {
            ShopUI_InventoryItemView itemView = UI.InventoryView_Viewport_Content.GameObject.transform.GetChild(i).GetComponent<ShopUI_InventoryItemView>();
            itemView.Rebind();

            if (_inventoryItemViews.Count < itemCount)
            {
                itemView.gameObject.SetActive(true);
                _inventoryItemViews.Add(itemView);
            }
            else
            {
                itemView.gameObject.SetActive(false);
            }
        }

        while (_inventoryItemViews.Count < itemCount)
        {
            GameObject clone = Instantiate(UI.InventoryView_Viewport_Content_InventoryItem.GameObject, UI.InventoryView_Viewport_Content.GameObject.transform);
            clone.name = UI.InventoryView_Viewport_Content_InventoryItem.GameObject.name;
            clone.SetActive(true);

            ShopUI_InventoryItemView itemView = clone.GetComponent<ShopUI_InventoryItemView>();
            itemView.Rebind();
            _inventoryItemViews.Add(itemView);
        }
    }

    private void OnModelChanged(CommonGameEvent gameEvent)
    {
        ShopUIModel eventModel = gameEvent.GetData<ShopUIModel>();
        if (eventModel != _model)
            return;

        Render();
    }

    private void SubscribeModelEvents()
    {
        if (_isModelEventSubscribed || _model == null)
            return;

        EventComponent.Instance.Subscribe(new CommonGameEvent(ShopUIModel.DataChangedEventName), OnModelChanged);
        _isModelEventSubscribed = true;
    }

    private void UnsubscribeModelEvents()
    {
        if (!_isModelEventSubscribed)
            return;

        EventComponent.Instance.Unsubscribe(new CommonGameEvent(ShopUIModel.DataChangedEventName), OnModelChanged);
        _isModelEventSubscribed = false;
    }
}
