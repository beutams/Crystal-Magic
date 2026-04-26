using System.Collections.Generic;
using System;
using CrystalMagic.Core;
using CrystalMagic.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ShopUI : UIBase<ShopUIData>
{
    private readonly List<ShopUI_CommodityItemView> _commodityItemViews = new();
    private readonly List<ShopUI_InventoryItemView> _inventoryItemViews = new();

    private ShopUIModel _model;
    private bool _isOpened;
    private bool _isModelEventSubscribed;
    private Coroutine _commodityHoverCoroutine;
    private ShopCommodityDisplayData _hoveredCommodity;
    private ShopCommodityDisplayData _draggedCommodity;
    private ShopInventoryDisplayData _draggedInventoryItem;
    private bool _dragRaycastDisabled;

    public event Action<ShopCommodityDisplayData> CommodityHoverReady;
    public event Action<ShopCommodityDisplayData> CommodityBuyRequested;
    public event Action<ShopInventoryDisplayData> InventorySellRequested;
    public event Action CommodityHoverExited;

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
        EnsureDragVisualInitialized();
        SetDragVisible(false);

        if (_model != null)
            Render();
    }

    public override void OnClose()
    {
        CancelCommodityHover(true);
        _draggedCommodity = null;
        _draggedInventoryItem = null;
        SetDragVisible(false);
        UnsubscribeModelEvents();
        _isOpened = false;
    }

    private void Render()
    {
        if (_model == null)
            return;

        RenderCommodities(_model.Commodities);
        RenderInventory(_model.InventoryItems, _model.InventorySlotCount);
        RenderMoney(_model.Money);
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

    private void RenderMoney(long money)
    {
        if (UI.Coin_MoneyText.TextMeshProUGUI != null)
            UI.Coin_MoneyText.TextMeshProUGUI.text = money.ToString();
    }

    private void EnsureCommodityItemViews(int itemCount)
    {
        _commodityItemViews.Clear();

        for (int i = 0; i < UI.ShopView_Viewport_Content.GameObject.transform.childCount; i++)
        {
            ShopUI_CommodityItemView itemView = UI.ShopView_Viewport_Content.GameObject.transform.GetChild(i).GetComponent<ShopUI_CommodityItemView>();
            itemView.Rebind();
            BindCommodityItemView(itemView);

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
            BindCommodityItemView(itemView);
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
            BindInventoryItemView(itemView);

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
            BindInventoryItemView(itemView);
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

    private void BindCommodityItemView(ShopUI_CommodityItemView itemView)
    {
        if (itemView == null)
            return;

        itemView.HoverEntered -= HandleCommodityHoverEntered;
        itemView.HoverExited -= HandleCommodityHoverExited;
        itemView.DoubleClicked -= HandleCommodityDoubleClicked;
        itemView.DragStarted -= HandleCommodityDragStarted;
        itemView.Dragging -= HandleCommodityDragging;
        itemView.DragEnded -= HandleCommodityDragEnded;
        itemView.HoverEntered += HandleCommodityHoverEntered;
        itemView.HoverExited += HandleCommodityHoverExited;
        itemView.DoubleClicked += HandleCommodityDoubleClicked;
        itemView.DragStarted += HandleCommodityDragStarted;
        itemView.Dragging += HandleCommodityDragging;
        itemView.DragEnded += HandleCommodityDragEnded;
    }

    private void BindInventoryItemView(ShopUI_InventoryItemView itemView)
    {
        if (itemView == null)
            return;

        itemView.DragStarted -= HandleInventoryDragStarted;
        itemView.Dragging -= HandleInventoryDragging;
        itemView.DragEnded -= HandleInventoryDragEnded;
        itemView.DragStarted += HandleInventoryDragStarted;
        itemView.Dragging += HandleInventoryDragging;
        itemView.DragEnded += HandleInventoryDragEnded;
    }

    private void HandleCommodityHoverEntered(ShopCommodityDisplayData data)
    {
        _hoveredCommodity = data;

        if (_commodityHoverCoroutine != null)
        {
            StopCoroutine(_commodityHoverCoroutine);
            _commodityHoverCoroutine = null;
        }

        CommodityHoverExited?.Invoke();
        _commodityHoverCoroutine = StartCoroutine(CommodityHoverDelayRoutine(data));
    }

    private void HandleCommodityHoverExited(ShopCommodityDisplayData data)
    {
        if (!ReferenceEquals(_hoveredCommodity, data))
            return;

        CancelCommodityHover(true);
    }

    private void HandleCommodityDoubleClicked(ShopCommodityDisplayData data)
    {
        if (data == null)
            return;

        CancelCommodityHover(true);
        CommodityBuyRequested?.Invoke(data);
    }

    private void HandleCommodityDragStarted(ShopCommodityDisplayData data, PointerEventData eventData)
    {
        if (data == null || eventData == null)
            return;

        CancelCommodityHover(true);
        _draggedInventoryItem = null;
        _draggedCommodity = data;
        UI.Drag_Icon.Image.sprite = LoadIcon(data.IconPath);
        SetDragVisible(true);
        UpdateDragPosition(eventData);
    }

    private void HandleCommodityDragging(ShopCommodityDisplayData data, PointerEventData eventData)
    {
        if (eventData == null || !ReferenceEquals(_draggedCommodity, data))
            return;

        UpdateDragPosition(eventData);
    }

    private void HandleCommodityDragEnded(ShopCommodityDisplayData data, PointerEventData eventData)
    {
        bool shouldOpenBuyUI = data != null
            && ReferenceEquals(_draggedCommodity, data)
            && IsPointerOverInventory(eventData);

        _draggedCommodity = null;
        SetDragVisible(false);

        if (shouldOpenBuyUI)
            CommodityBuyRequested?.Invoke(data);
    }

    private void HandleInventoryDragStarted(ShopInventoryDisplayData data, PointerEventData eventData)
    {
        if (data == null || eventData == null)
            return;

        CancelCommodityHover(true);
        _draggedCommodity = null;
        _draggedInventoryItem = data;
        UI.Drag_Icon.Image.sprite = LoadIcon(data.IconPath);
        SetDragVisible(true);
        UpdateDragPosition(eventData);
    }

    private void HandleInventoryDragging(ShopInventoryDisplayData data, PointerEventData eventData)
    {
        if (eventData == null || !ReferenceEquals(_draggedInventoryItem, data))
            return;

        UpdateDragPosition(eventData);
    }

    private void HandleInventoryDragEnded(ShopInventoryDisplayData data, PointerEventData eventData)
    {
        bool shouldOpenSellUI = data != null
            && ReferenceEquals(_draggedInventoryItem, data)
            && IsPointerOverShop(eventData);

        _draggedInventoryItem = null;
        SetDragVisible(false);

        if (shouldOpenSellUI)
            InventorySellRequested?.Invoke(data);
    }

    private System.Collections.IEnumerator CommodityHoverDelayRoutine(ShopCommodityDisplayData data)
    {
        yield return new WaitForSeconds(1f);
        _commodityHoverCoroutine = null;

        if (!ReferenceEquals(_hoveredCommodity, data))
            yield break;

        CommodityHoverReady?.Invoke(data);
    }

    private void CancelCommodityHover(bool closeInfo)
    {
        _hoveredCommodity = null;

        if (_commodityHoverCoroutine != null)
        {
            StopCoroutine(_commodityHoverCoroutine);
            _commodityHoverCoroutine = null;
        }

        if (closeInfo)
            CommodityHoverExited?.Invoke();
    }

    private void EnsureDragVisualInitialized()
    {
        if (_dragRaycastDisabled || UI.Drag.GameObject == null)
            return;

        Graphic[] graphics = UI.Drag.GameObject.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;

        _dragRaycastDisabled = true;
    }

    private void SetDragVisible(bool visible)
    {
        if (UI.Drag.GameObject == null)
            return;

        if (UI.Drag.GameObject.activeSelf != visible)
            UI.Drag.GameObject.SetActive(visible);
    }

    private void UpdateDragPosition(PointerEventData eventData)
    {
        if (eventData == null || UI.Drag.RectTransform == null)
            return;

        RectTransform parentRect = UI.Drag.RectTransform.parent as RectTransform;
        if (parentRect == null)
        {
            UI.Drag.RectTransform.position = eventData.position;
            return;
        }

        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventCamera, out Vector2 localPoint))
            UI.Drag.RectTransform.anchoredPosition = localPoint;
    }

    private bool IsPointerOverInventory(PointerEventData eventData)
    {
        if (eventData == null || UI.InventoryView.RectTransform == null)
            return false;

        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(UI.InventoryView.RectTransform, eventData.position, eventCamera);
    }

    private bool IsPointerOverShop(PointerEventData eventData)
    {
        if (eventData == null || UI.ShopView.RectTransform == null)
            return false;

        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(UI.ShopView.RectTransform, eventData.position, eventCamera);
    }

    private Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return ResourceComponent.Instance.Load<Sprite>(iconPath);
    }
}
