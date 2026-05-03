using CrystalMagic.Core;
using CrystalMagic.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class StashUI : UIBase<StashUIData, CrystalMagic.UI.StashUIModel>
{
    private readonly List<StashUI_InventoryItemView> _inventoryItemViews = new();
    private readonly List<StashUI_StashItemView> _stashItemViews = new();
    private StashInventoryDisplayData _draggedInventoryItem;
    private StashItemDisplayData _draggedStashItem;
    private bool _dragRaycastDisabled;

    public event Action AllCategoryRequested;
    public event Action SkillCategoryRequested;
    public event Action EquipCategoryRequested;
    public event Action PropsCategoryRequested;
    public event Action<StashInventoryDisplayData> InventoryStoreRequested;
    public event Action<StashItemDisplayData> StashWithdrawRequested;

    public override void OnOpen()
    {
        EnsureDragVisualInitialized();
        SetDragVisible(false);
        UI.ButtonList_All.ButtonPlus.onClick.AddListener(OnAllCategoryButton);
        UI.ButtonList_Skill.ButtonPlus.onClick.AddListener(OnSkillCategoryButton);
        UI.ButtonList_Equip.ButtonPlus.onClick.AddListener(OnEquipCategoryButton);
        UI.ButtonList_Props.ButtonPlus.onClick.AddListener(OnPropsCategoryButton);
        base.OnOpen();
    }

    public override void OnClose()
    {
        UI.ButtonList_All.ButtonPlus.onClick.RemoveListener(OnAllCategoryButton);
        UI.ButtonList_Skill.ButtonPlus.onClick.RemoveListener(OnSkillCategoryButton);
        UI.ButtonList_Equip.ButtonPlus.onClick.RemoveListener(OnEquipCategoryButton);
        UI.ButtonList_Props.ButtonPlus.onClick.RemoveListener(OnPropsCategoryButton);
        _draggedInventoryItem = null;
        _draggedStashItem = null;
        SetDragVisible(false);
        UISubViewBase.ReleaseAllToPool(_inventoryItemViews);
        UISubViewBase.ReleaseAllToPool(_stashItemViews);
        base.OnClose();
    }

    protected override void RefreshView()
    {
        if (Model == null)
            return;

        RenderInventory(Model.InventoryItems, Model.InventorySlotCount);
        RenderStash(Model.StashItems);
        UI.Coin_MoneyText.TextMeshProUGUI.text = Model.StashMoney.ToString();
        RefreshCategorySelection();
    }

    private void RenderInventory(CrystalMagic.UI.StashInventoryDisplayData[] inventoryItems, int slotCount)
    {
        EnsureInventoryItemViews(slotCount);

        for (int i = 0; i < _inventoryItemViews.Count; i++)
        {
            CrystalMagic.UI.StashInventoryDisplayData data = inventoryItems != null && i < inventoryItems.Length ? inventoryItems[i] : null;
            _inventoryItemViews[i].Render(data);
        }
    }

    private void RenderStash(System.Collections.Generic.IReadOnlyList<CrystalMagic.UI.StashItemDisplayData> stashItems)
    {
        int stashItemCount = stashItems != null ? stashItems.Count : 0;
        EnsureStashItemViews(stashItemCount);

        for (int i = 0; i < _stashItemViews.Count; i++)
        {
            CrystalMagic.UI.StashItemDisplayData data = stashItems != null && i < stashItems.Count ? stashItems[i] : null;
            _stashItemViews[i].Render(data);
        }
    }

    private void EnsureInventoryItemViews(int itemCount)
    {
        UI.InventoryView_Viewport_Content_InventoryItem.GameObject.SetActive(false);

        while (_inventoryItemViews.Count > itemCount)
        {
            int lastIndex = _inventoryItemViews.Count - 1;
            StashUI_InventoryItemView itemView = _inventoryItemViews[lastIndex];
            UISubViewBase.ReleaseToPool(itemView);
            _inventoryItemViews.RemoveAt(lastIndex);
        }

        while (_inventoryItemViews.Count < itemCount)
        {
            StashUI_InventoryItemView itemView = UISubViewBase.AcquireFromPool(UI.InventoryView_Viewport_Content_InventoryItem.GameObject.GetComponent<StashUI_InventoryItemView>(), UI.InventoryView_Viewport_Content.GameObject.transform);
            if (itemView == null)
                break;

            BindInventoryItemView(itemView);
            _inventoryItemViews.Add(itemView);
        }
    }

    private void EnsureStashItemViews(int itemCount)
    {
        UI.StashView_Viewport_Content_StashItem.GameObject.SetActive(false);

        while (_stashItemViews.Count > itemCount)
        {
            int lastIndex = _stashItemViews.Count - 1;
            StashUI_StashItemView itemView = _stashItemViews[lastIndex];
            UISubViewBase.ReleaseToPool(itemView);
            _stashItemViews.RemoveAt(lastIndex);
        }

        while (_stashItemViews.Count < itemCount)
        {
            StashUI_StashItemView itemView = UISubViewBase.AcquireFromPool(UI.StashView_Viewport_Content_StashItem.GameObject.GetComponent<StashUI_StashItemView>(), UI.StashView_Viewport_Content.GameObject.transform);
            if (itemView == null)
                break;

            BindStashItemView(itemView);
            _stashItemViews.Add(itemView);
        }
    }

    private void BindInventoryItemView(StashUI_InventoryItemView itemView)
    {
        if (itemView == null)
            return;

        itemView.DragStarted -= HandleInventoryDragStarted;
        itemView.Dragging -= HandleInventoryDragging;
        itemView.DragEnded -= HandleInventoryDragEnded;
        itemView.DoubleClicked -= HandleInventoryDoubleClicked;
        itemView.DragStarted += HandleInventoryDragStarted;
        itemView.Dragging += HandleInventoryDragging;
        itemView.DragEnded += HandleInventoryDragEnded;
        itemView.DoubleClicked += HandleInventoryDoubleClicked;
    }

    private void BindStashItemView(StashUI_StashItemView itemView)
    {
        if (itemView == null)
            return;

        itemView.DragStarted -= HandleStashDragStarted;
        itemView.Dragging -= HandleStashDragging;
        itemView.DragEnded -= HandleStashDragEnded;
        itemView.DoubleClicked -= HandleStashDoubleClicked;
        itemView.DragStarted += HandleStashDragStarted;
        itemView.Dragging += HandleStashDragging;
        itemView.DragEnded += HandleStashDragEnded;
        itemView.DoubleClicked += HandleStashDoubleClicked;
    }

    private void RefreshCategorySelection()
    {
        SetCategorySelected(UI.ButtonList_All.GameObject, UI.ButtonList_All_Default.GameObject, UI.ButtonList_All_Select.GameObject, Model.Category == CrystalMagic.UI.StashCategory.All);
        SetCategorySelected(UI.ButtonList_Skill.GameObject, UI.ButtonList_Skill_Default.GameObject, UI.ButtonList_Skill_Select.GameObject, Model.Category == CrystalMagic.UI.StashCategory.Skill);
        SetCategorySelected(UI.ButtonList_Equip.GameObject, UI.ButtonList_Equip_Default.GameObject, UI.ButtonList_Equip_Select.GameObject, Model.Category == CrystalMagic.UI.StashCategory.Equip);
        SetCategorySelected(UI.ButtonList_Props.GameObject, UI.ButtonList_Props_Default.GameObject, UI.ButtonList_Props_Select.GameObject, Model.Category == CrystalMagic.UI.StashCategory.Props);
    }

    private void SetCategorySelected(UnityEngine.GameObject buttonObject, UnityEngine.GameObject defaultObject, UnityEngine.GameObject selectedObject, bool selected)
    {
        UISelectableListItem selectable = buttonObject != null ? buttonObject.GetComponent<UISelectableListItem>() : null;
        if (selectable != null)
        {
            selectable.SetSelected(selected);
            return;
        }

        if (defaultObject != null)
            defaultObject.SetActive(!selected);

        if (selectedObject != null)
            selectedObject.SetActive(selected);
    }

    private void OnAllCategoryButton() => AllCategoryRequested?.Invoke();
    private void OnSkillCategoryButton() => SkillCategoryRequested?.Invoke();
    private void OnEquipCategoryButton() => EquipCategoryRequested?.Invoke();
    private void OnPropsCategoryButton() => PropsCategoryRequested?.Invoke();

    private void HandleInventoryDragStarted(StashInventoryDisplayData data, PointerEventData eventData)
    {
        if (data == null || eventData == null)
            return;

        _draggedStashItem = null;
        _draggedInventoryItem = data;
        if (UI.Drag_Icon.Image != null)
            UI.Drag_Icon.Image.sprite = LoadIcon(data.IconPath);
        SetDragVisible(true);
        UpdateDragPosition(eventData);
    }

    private void HandleInventoryDragging(StashInventoryDisplayData data, PointerEventData eventData)
    {
        if (eventData == null || !ReferenceEquals(_draggedInventoryItem, data))
            return;

        UpdateDragPosition(eventData);
    }

    private void HandleInventoryDragEnded(StashInventoryDisplayData data, PointerEventData eventData)
    {
        bool shouldOpenInteractUI = data != null
            && ReferenceEquals(_draggedInventoryItem, data)
            && IsPointerOverRect(UI.StashView.RectTransform, eventData);

        _draggedInventoryItem = null;
        SetDragVisible(false);

        if (shouldOpenInteractUI)
            InventoryStoreRequested?.Invoke(data);
    }

    private void HandleInventoryDoubleClicked(StashInventoryDisplayData data)
    {
        if (data == null)
            return;

        InventoryStoreRequested?.Invoke(data);
    }

    private void HandleStashDragStarted(StashItemDisplayData data, PointerEventData eventData)
    {
        if (data == null || eventData == null)
            return;

        _draggedInventoryItem = null;
        _draggedStashItem = data;
        if (UI.Drag_Icon.Image != null)
            UI.Drag_Icon.Image.sprite = LoadIcon(data.IconPath);
        SetDragVisible(true);
        UpdateDragPosition(eventData);
    }

    private void HandleStashDragging(StashItemDisplayData data, PointerEventData eventData)
    {
        if (eventData == null || !ReferenceEquals(_draggedStashItem, data))
            return;

        UpdateDragPosition(eventData);
    }

    private void HandleStashDragEnded(StashItemDisplayData data, PointerEventData eventData)
    {
        bool shouldOpenInteractUI = data != null
            && ReferenceEquals(_draggedStashItem, data)
            && IsPointerOverRect(UI.InventoryView.RectTransform, eventData);

        _draggedStashItem = null;
        SetDragVisible(false);

        if (shouldOpenInteractUI)
            StashWithdrawRequested?.Invoke(data);
    }

    private void HandleStashDoubleClicked(StashItemDisplayData data)
    {
        if (data == null)
            return;

        StashWithdrawRequested?.Invoke(data);
    }

    private void EnsureDragVisualInitialized()
    {
        if (_dragRaycastDisabled || UI.Drag.GameObject == null)
            return;

        UnityEngine.UI.Graphic[] graphics = UI.Drag.GameObject.GetComponentsInChildren<UnityEngine.UI.Graphic>(true);
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

    private bool IsPointerOverRect(RectTransform rectTransform, PointerEventData eventData)
    {
        if (rectTransform == null || eventData == null)
            return false;

        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, eventData.position, eventCamera);
    }

    private Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return LoadManagedResource<Sprite>(iconPath);
    }

}
