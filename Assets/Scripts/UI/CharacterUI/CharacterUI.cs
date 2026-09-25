using CrystalMagic.Core;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CharacterUI : UIBase<CharacterUIData, CrystalMagic.UI.CharacterUIModel>
{
    private readonly List<CharacterUI_SkillItemView> _skillItemViews = new();
    private readonly List<CharacterUI_InventoryItemView> _inventoryItemViews = new();
    private readonly List<CrystalMagic.UI.CharacterSkillDisplayData> _currentSkillItems = new();
    private readonly CrystalMagic.UI.CharacterEquipDisplayData[] _currentEquipItems = new CrystalMagic.UI.CharacterEquipDisplayData[5];
    private readonly CrystalMagic.UI.CharacterPropDisplayData[] _currentPropItems = new CrystalMagic.UI.CharacterPropDisplayData[3];

    private bool _itemDragRaycastDisabled;
    private bool _skillDragRaycastDisabled;
    private CrystalMagic.UI.CharacterInventoryDisplayData _draggedInventoryItem;
    private CrystalMagic.UI.CharacterEquipDisplayData _draggedEquipItem;
    private CrystalMagic.UI.CharacterSkillDisplayData _draggedSkillItem;
    private CrystalMagic.UI.CharacterPropDisplayData _draggedPropItem;

    public event Action<CrystalMagic.UI.CharacterInventoryDisplayData, int> InventorySkillStoneDropped;
    public event Action<CrystalMagic.UI.CharacterInventoryDisplayData, int> InventoryEquipDropped;
    public event Action<CrystalMagic.UI.CharacterInventoryDisplayData, int> InventoryItemMoved;
    public event Action<CrystalMagic.UI.CharacterInventoryDisplayData, int> InventoryPropDropped;
    public event Action<int, int> EquipReturnedToInventory;
    public event Action<int, int> SpiritEquipSwapped;
    public event Action<CrystalMagic.UI.CharacterSkillDisplayData> SkillAdditionRequested;
    public event Action<CrystalMagic.UI.CharacterSkillDisplayData, int> SkillReordered;
    public event Action<CrystalMagic.UI.CharacterSkillDisplayData, int> SkillReturnedToInventory;
    public event Action<int, int> PropReturnedToInventory;
    public event Action<int, int> PropSlotMoved;

    public override void OnOpen()
    {
        EnsureEquipSlotHandlers();
        EnsurePropSlotHandlers();
        EnsureItemDragInitialized();
        EnsureSkillDragInitialized();
        SetItemDragVisible(false);
        SetSkillDragVisible(false);
        base.OnOpen();
    }

    public override void OnClose()
    {
        _draggedInventoryItem = null;
        _draggedEquipItem = null;
        _draggedSkillItem = null;
        _draggedPropItem = null;
        SetItemDragVisible(false);
        SetSkillDragVisible(false);
        UISubViewBase.ReleaseAllToPool(_skillItemViews);
        UISubViewBase.ReleaseAllToPool(_inventoryItemViews);
        base.OnClose();
    }

    protected override void RefreshView()
    {
        if (Model == null)
            return;

        RenderSkill(Model.SkillItems);
        RenderInventory(Model.InventoryItems, Model.InventorySlotCount);
        RenderEquip(Model.EquipItems);
        RenderProps(Model.PropItems);
    }

    private void RenderSkill(IReadOnlyList<CrystalMagic.UI.CharacterSkillDisplayData> skillItems)
    {
        _currentSkillItems.Clear();
        int skillItemCount = skillItems != null ? skillItems.Count : 0;
        EnsureSkillItemViews(skillItemCount);

        for (int i = 0; i < _skillItemViews.Count; i++)
        {
            CrystalMagic.UI.CharacterSkillDisplayData data = skillItems != null && i < skillItems.Count ? skillItems[i] : null;
            if (data != null)
                _currentSkillItems.Add(data);
            _skillItemViews[i].Render(data);
        }
    }

    private void RenderInventory(IReadOnlyList<CrystalMagic.UI.CharacterInventoryDisplayData> inventoryItems, int slotCount)
    {
        EnsureInventoryItemViews(slotCount);

        for (int i = 0; i < _inventoryItemViews.Count; i++)
        {
            CrystalMagic.UI.CharacterInventoryDisplayData data = inventoryItems != null && i < inventoryItems.Count ? inventoryItems[i] : null;
            _inventoryItemViews[i].Render(data, i);
        }
    }

    private void RenderEquip(CrystalMagic.UI.CharacterEquipDisplayData[] equipItems)
    {
        for (int i = 0; i < _currentEquipItems.Length; i++)
            _currentEquipItems[i] = equipItems != null && i < equipItems.Length ? equipItems[i] : null;

        RenderEquipSlot(UI.Equip_MagicStoneBorder_MagicStone, _currentEquipItems[0]);
        RenderEquipSlot(UI.Equip_Equip1Border_Equip1, _currentEquipItems[1]);
        RenderEquipSlot(UI.Equip_Equip2Border_Equip2, _currentEquipItems[2]);
        RenderEquipSlot(UI.Equip_Equip3Border_Equip3, _currentEquipItems[3]);
        RenderEquipSlot(UI.Equip_Equip4Border_Equip4, _currentEquipItems[4]);
    }

    private void RenderProps(CrystalMagic.UI.CharacterPropDisplayData[] propItems)
    {
        for (int i = 0; i < _currentPropItems.Length; i++)
            _currentPropItems[i] = propItems != null && i < propItems.Length ? propItems[i] : null;

        RenderPropSlot(UI.PropSlots_PropSlot1_Icon, UI.PropSlots_PropSlot1_Count, _currentPropItems[0]);
        RenderPropSlot(UI.PropSlots_PropSlot2_Icon, UI.PropSlots_PropSlot2_Count, _currentPropItems[1]);
        RenderPropSlot(UI.PropSlots_PropSlot3_Icon, UI.PropSlots_PropSlot3_Count, _currentPropItems[2]);
    }

    private void RenderPropSlot(UINode iconNode, UINode countNode, CrystalMagic.UI.CharacterPropDisplayData data)
    {
        iconNode.Image.sprite = LoadIcon(data != null ? data.IconPath : string.Empty);
        iconNode.Image.color = data != null ? Color.white : new Color(1f, 1f, 1f, 0.2f);
        countNode.TextMeshProUGUI.text = data != null && data.Count > 0 ? data.Count.ToString() : string.Empty;
    }

    private void RenderEquipSlot(UINode node, CrystalMagic.UI.CharacterEquipDisplayData data)
    {
        Sprite icon = LoadIcon(data != null ? data.IconPath : string.Empty);
        node.Image.sprite = icon;
        node.Image.color = data != null ? Color.white : new Color(1f, 1f, 1f, 0.2f);
    }

    private void EnsureSkillItemViews(int itemCount)
    {
        UI.Skill_SkillChain_Viewport_Content_SkillItem.GameObject.SetActive(false);

        while (_skillItemViews.Count > itemCount)
        {
            int lastIndex = _skillItemViews.Count - 1;
            CharacterUI_SkillItemView itemView = _skillItemViews[lastIndex];
            UISubViewBase.ReleaseToPool(itemView);
            _skillItemViews.RemoveAt(lastIndex);
        }

        CharacterUI_SkillItemView templateView = UI.Skill_SkillChain_Viewport_Content_SkillItem.GameObject.GetComponent<CharacterUI_SkillItemView>();
        if (templateView == null)
            return;

        UISubViewBase.EnsurePoolCapacity(templateView, itemCount, itemCount);

        while (_skillItemViews.Count < itemCount)
        {
            CharacterUI_SkillItemView itemView = UISubViewBase.AcquireFromPool(templateView, UI.Skill_SkillChain_Viewport_Content.GameObject.transform);
            if (itemView == null)
                break;

            BindSkillItemView(itemView);
            _skillItemViews.Add(itemView);
        }
    }

    private void EnsureInventoryItemViews(int itemCount)
    {
        UI.InventoryView_Viewport_Content_InventoryItem.GameObject.SetActive(false);

        while (_inventoryItemViews.Count > itemCount)
        {
            int lastIndex = _inventoryItemViews.Count - 1;
            CharacterUI_InventoryItemView itemView = _inventoryItemViews[lastIndex];
            UISubViewBase.ReleaseToPool(itemView);
            _inventoryItemViews.RemoveAt(lastIndex);
        }

        CharacterUI_InventoryItemView templateView = UI.InventoryView_Viewport_Content_InventoryItem.GameObject.GetComponent<CharacterUI_InventoryItemView>();
        if (templateView == null)
            return;

        UISubViewBase.EnsurePoolCapacity(templateView, itemCount, itemCount);

        while (_inventoryItemViews.Count < itemCount)
        {
            CharacterUI_InventoryItemView itemView = UISubViewBase.AcquireFromPool(templateView, UI.InventoryView_Viewport_Content.GameObject.transform);
            if (itemView == null)
                break;

            BindInventoryItemView(itemView);
            _inventoryItemViews.Add(itemView);
        }
    }

    private void BindInventoryItemView(CharacterUI_InventoryItemView itemView)
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

    private void BindSkillItemView(CharacterUI_SkillItemView itemView)
    {
        if (itemView == null)
            return;

        itemView.DragStarted -= HandleSkillDragStarted;
        itemView.Dragging -= HandleSkillDragging;
        itemView.DragEnded -= HandleSkillDragEnded;
        itemView.AdditionClicked -= HandleSkillAdditionClicked;
        itemView.DragStarted += HandleSkillDragStarted;
        itemView.Dragging += HandleSkillDragging;
        itemView.DragEnded += HandleSkillDragEnded;
        itemView.AdditionClicked += HandleSkillAdditionClicked;
    }

    private void EnsureEquipSlotHandlers()
    {
        BindEquipSlotHandler(0, UI.Equip_MagicStoneBorder.GameObject);
        BindEquipSlotHandler(1, UI.Equip_Equip1Border.GameObject);
        BindEquipSlotHandler(2, UI.Equip_Equip2Border.GameObject);
        BindEquipSlotHandler(3, UI.Equip_Equip3Border.GameObject);
        BindEquipSlotHandler(4, UI.Equip_Equip4Border.GameObject);
    }

    private void EnsurePropSlotHandlers()
    {
        BindPropSlotHandler(0, UI.PropSlots_PropSlot1.GameObject);
        BindPropSlotHandler(1, UI.PropSlots_PropSlot2.GameObject);
        BindPropSlotHandler(2, UI.PropSlots_PropSlot3.GameObject);
    }

    private void BindPropSlotHandler(int slotIndex, GameObject target)
    {
        CharacterUI_PropSlotDragHandler handler = target.GetComponent<CharacterUI_PropSlotDragHandler>();
        handler.Initialize(slotIndex);
        handler.DragStarted -= HandlePropDragStarted;
        handler.Dragging -= HandlePropDragging;
        handler.DragEnded -= HandlePropDragEnded;
        handler.DragStarted += HandlePropDragStarted;
        handler.Dragging += HandlePropDragging;
        handler.DragEnded += HandlePropDragEnded;
    }

    private void BindEquipSlotHandler(int slotIndex, GameObject target)
    {
        if (target == null)
            return;

        CharacterUI_EquipSlotDragHandler handler = target.GetComponent<CharacterUI_EquipSlotDragHandler>();
        if (handler == null)
            handler = target.AddComponent<CharacterUI_EquipSlotDragHandler>();

        handler.Initialize(slotIndex);
        handler.DragStarted -= HandleEquipDragStarted;
        handler.Dragging -= HandleEquipDragging;
        handler.DragEnded -= HandleEquipDragEnded;
        handler.DragStarted += HandleEquipDragStarted;
        handler.Dragging += HandleEquipDragging;
        handler.DragEnded += HandleEquipDragEnded;
    }

    private void HandleInventoryDragStarted(CrystalMagic.UI.CharacterInventoryDisplayData data, PointerEventData eventData)
    {
        if (data == null || eventData == null)
            return;

        _draggedEquipItem = null;
        _draggedSkillItem = null;
        _draggedPropItem = null;
        _draggedInventoryItem = data;
        UI.ItemDrag_Mask_Icon.Image.sprite = LoadIcon(data.IconPath);
        SetItemDragVisible(true);
        UpdateItemDragPosition(eventData);
    }

    private void HandleInventoryDragging(CrystalMagic.UI.CharacterInventoryDisplayData data, PointerEventData eventData)
    {
        if (eventData == null || !ReferenceEquals(_draggedInventoryItem, data))
            return;

        UpdateItemDragPosition(eventData);
    }

    private void HandleInventoryDragEnded(CrystalMagic.UI.CharacterInventoryDisplayData data, PointerEventData eventData)
    {
        if (data == null || !ReferenceEquals(_draggedInventoryItem, data))
        {
            _draggedInventoryItem = null;
            SetItemDragVisible(false);
            return;
        }

        int skillInsertIndex = GetSkillInsertIndex(eventData);
        if (data.ItemType == CrystalMagic.Game.Data.ItemType.SkillStone && skillInsertIndex >= 0)
        {
            InventorySkillStoneDropped?.Invoke(data, skillInsertIndex);
        }
        else if (TryGetHoveredEquipSlotIndex(eventData, out int equipSlotIndex))
        {
            InventoryEquipDropped?.Invoke(data, equipSlotIndex);
        }
        else if (data.ItemType == CrystalMagic.Game.Data.ItemType.Prop &&
                 TryGetHoveredPropSlotIndex(eventData, out int propSlotIndex))
        {
            InventoryPropDropped?.Invoke(data, propSlotIndex);
        }
        else if (TryGetHoveredInventorySlotIndex(eventData, out int inventorySlotIndex))
        {
            InventoryItemMoved?.Invoke(data, inventorySlotIndex);
        }

        _draggedInventoryItem = null;
        SetItemDragVisible(false);
    }

    private void HandleEquipDragStarted(int slotIndex, PointerEventData eventData)
    {
        if (eventData == null || slotIndex < 0 || slotIndex >= _currentEquipItems.Length)
            return;

        CrystalMagic.UI.CharacterEquipDisplayData data = _currentEquipItems[slotIndex];
        if (data == null || data.ItemId < 0 || data.ItemType == CrystalMagic.Game.Data.ItemType.None)
            return;

        _draggedInventoryItem = null;
        _draggedSkillItem = null;
        _draggedPropItem = null;
        _draggedEquipItem = data;
        UI.ItemDrag_Mask_Icon.Image.sprite = LoadIcon(data.IconPath);
        SetItemDragVisible(true);
        UpdateItemDragPosition(eventData);
    }

    private void HandleEquipDragging(int slotIndex, PointerEventData eventData)
    {
        if (eventData == null || _draggedEquipItem == null || _draggedEquipItem.SlotIndex != slotIndex)
            return;

        UpdateItemDragPosition(eventData);
    }

    private void HandleEquipDragEnded(int slotIndex, PointerEventData eventData)
    {
        int hoveredSlotIndex = -1;
        bool shouldSwapSpiritSlot = _draggedEquipItem != null
            && _draggedEquipItem.SlotIndex == slotIndex
            && TryGetHoveredEquipSlotIndex(eventData, out hoveredSlotIndex)
            && slotIndex >= 1
            && slotIndex <= 4
            && hoveredSlotIndex >= 1
            && hoveredSlotIndex <= 4
            && hoveredSlotIndex != slotIndex;

        int inventorySlotIndex = -1;
        bool shouldReturnToInventory = _draggedEquipItem != null
            && _draggedEquipItem.SlotIndex == slotIndex
            && TryGetHoveredInventorySlotIndex(eventData, out inventorySlotIndex);

        _draggedEquipItem = null;
        SetItemDragVisible(false);

        if (shouldSwapSpiritSlot)
            SpiritEquipSwapped?.Invoke(slotIndex, hoveredSlotIndex);

        if (shouldReturnToInventory)
            EquipReturnedToInventory?.Invoke(slotIndex, inventorySlotIndex);
    }

    private void HandleSkillDragStarted(CrystalMagic.UI.CharacterSkillDisplayData data, PointerEventData eventData)
    {
        if (data == null || eventData == null)
            return;

        _draggedInventoryItem = null;
        _draggedEquipItem = null;
        _draggedPropItem = null;
        _draggedSkillItem = data;
        UI.SkillDrag_Mask_Icon.Image.sprite = LoadIcon(data.SkillIconPath);
        SetSkillDragVisible(true);
        UpdateSkillDragPosition(eventData);
    }

    private void HandleSkillDragging(CrystalMagic.UI.CharacterSkillDisplayData data, PointerEventData eventData)
    {
        if (eventData == null || !ReferenceEquals(_draggedSkillItem, data))
            return;

        UpdateSkillDragPosition(eventData);
    }

    private void HandleSkillDragEnded(CrystalMagic.UI.CharacterSkillDisplayData data, PointerEventData eventData)
    {
        if (data == null || !ReferenceEquals(_draggedSkillItem, data))
        {
            _draggedSkillItem = null;
            SetSkillDragVisible(false);
            return;
        }

        int skillInsertIndex = GetSkillInsertIndex(eventData);
        if (skillInsertIndex >= 0)
        {
            SkillReordered?.Invoke(data, skillInsertIndex);
        }
        else if (TryGetHoveredInventorySlotIndex(eventData, out int inventorySlotIndex))
        {
            SkillReturnedToInventory?.Invoke(data, inventorySlotIndex);
        }

        _draggedSkillItem = null;
        SetSkillDragVisible(false);
    }

    private void HandleSkillAdditionClicked(CrystalMagic.UI.CharacterSkillDisplayData data)
    {
        if (data == null)
            return;

        SkillAdditionRequested?.Invoke(data);
    }

    private void HandlePropDragStarted(int slotIndex, PointerEventData eventData)
    {
        if (eventData == null || slotIndex < 0 || slotIndex >= _currentPropItems.Length)
            return;

        CrystalMagic.UI.CharacterPropDisplayData data = _currentPropItems[slotIndex];
        if (data == null || data.ItemId < 0)
            return;

        _draggedInventoryItem = null;
        _draggedEquipItem = null;
        _draggedSkillItem = null;
        _draggedPropItem = data;
        UI.ItemDrag_Mask_Icon.Image.sprite = LoadIcon(data.IconPath);
        SetItemDragVisible(true);
        UpdateItemDragPosition(eventData);
    }

    private void HandlePropDragging(int slotIndex, PointerEventData eventData)
    {
        if (eventData == null || _draggedPropItem == null || _draggedPropItem.SlotIndex != slotIndex)
            return;

        UpdateItemDragPosition(eventData);
    }

    private void HandlePropDragEnded(int slotIndex, PointerEventData eventData)
    {
        bool validSource = _draggedPropItem != null && _draggedPropItem.SlotIndex == slotIndex;
        int targetPropSlotIndex = -1;
        int targetInventorySlotIndex = -1;
        bool moveToProp = validSource && TryGetHoveredPropSlotIndex(eventData, out targetPropSlotIndex) &&
                          targetPropSlotIndex != slotIndex;
        bool moveToInventory = validSource &&
                               TryGetHoveredInventorySlotIndex(eventData, out targetInventorySlotIndex);

        _draggedPropItem = null;
        SetItemDragVisible(false);

        if (moveToProp)
            PropSlotMoved?.Invoke(slotIndex, targetPropSlotIndex);
        else if (moveToInventory)
            PropReturnedToInventory?.Invoke(slotIndex, targetInventorySlotIndex);
    }

    private void EnsureItemDragInitialized()
    {
        DisableDragRaycasts(UI.ItemDrag.GameObject, ref _itemDragRaycastDisabled);
    }

    private void EnsureSkillDragInitialized()
    {
        DisableDragRaycasts(UI.SkillDrag.GameObject, ref _skillDragRaycastDisabled);
    }

    private static void DisableDragRaycasts(GameObject dragObject, ref bool raycastsDisabled)
    {
        if (raycastsDisabled)
            return;

        Graphic[] graphics = dragObject.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;

        raycastsDisabled = true;
    }

    private void SetItemDragVisible(bool visible)
    {
        SetDragVisible(UI.ItemDrag.GameObject, visible);
    }

    private void SetSkillDragVisible(bool visible)
    {
        SetDragVisible(UI.SkillDrag.GameObject, visible);
    }

    private static void SetDragVisible(GameObject dragObject, bool visible)
    {
        if (dragObject.activeSelf != visible)
            dragObject.SetActive(visible);
    }

    private void UpdateItemDragPosition(PointerEventData eventData)
    {
        UpdateDragPosition(UI.ItemDrag.RectTransform, eventData);
    }

    private void UpdateSkillDragPosition(PointerEventData eventData)
    {
        UpdateDragPosition(UI.SkillDrag.RectTransform, eventData);
    }

    private static void UpdateDragPosition(RectTransform dragRect, PointerEventData eventData)
    {
        if (eventData == null)
            return;

        RectTransform parentRect = dragRect.parent as RectTransform;
        if (parentRect == null)
        {
            dragRect.position = eventData.position;
            return;
        }

        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventCamera, out Vector2 localPoint))
            dragRect.anchoredPosition = localPoint;
    }

    private bool IsPointerOverSkillChain(PointerEventData eventData)
    {
        if (eventData == null)
            return false;

        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(UI.Skill_SkillChain.RectTransform, eventData.position, eventCamera);
    }

    private int GetSkillInsertIndex(PointerEventData eventData)
    {
        if (!IsPointerOverSkillChain(eventData))
            return -1;

        if (_skillItemViews.Count == 0)
            return 0;

        float pointerX = eventData.position.x;
        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;

        for (int i = 0; i < _skillItemViews.Count; i++)
        {
            RectTransform rectTransform = _skillItemViews[i].transform as RectTransform;
            if (rectTransform == null || !_skillItemViews[i].gameObject.activeInHierarchy)
                continue;

            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            float left = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]).x;
            float right = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[3]).x;
            float mid = (left + right) * 0.5f;

            if (pointerX < mid)
                return i;
        }

        return _currentSkillItems.Count;
    }

    private bool TryGetHoveredInventorySlotIndex(PointerEventData eventData, out int slotIndex)
    {
        slotIndex = -1;
        GameObject hovered = eventData?.pointerCurrentRaycast.gameObject;
        CharacterUI_InventoryItemView itemView = hovered != null
            ? hovered.GetComponentInParent<CharacterUI_InventoryItemView>()
            : null;
        if (itemView == null)
            return false;

        slotIndex = itemView.SlotIndex;
        return slotIndex >= 0;
    }

    private bool TryGetHoveredPropSlotIndex(PointerEventData eventData, out int slotIndex)
    {
        slotIndex = -1;
        GameObject hovered = eventData?.pointerCurrentRaycast.gameObject;
        CharacterUI_PropSlotDragHandler handler = hovered != null
            ? hovered.GetComponentInParent<CharacterUI_PropSlotDragHandler>()
            : null;
        if (handler == null)
            return false;

        slotIndex = handler.SlotIndex;
        return slotIndex >= 0;
    }

    private bool TryGetHoveredEquipSlotIndex(PointerEventData eventData, out int slotIndex)
    {
        slotIndex = -1;
        if (eventData == null)
            return false;

        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        if (Contains(UI.Equip_MagicStoneBorder.RectTransform, eventData.position, eventCamera))
        {
            slotIndex = 0;
            return true;
        }

        if (Contains(UI.Equip_Equip1Border.RectTransform, eventData.position, eventCamera))
        {
            slotIndex = 1;
            return true;
        }

        if (Contains(UI.Equip_Equip2Border.RectTransform, eventData.position, eventCamera))
        {
            slotIndex = 2;
            return true;
        }

        if (Contains(UI.Equip_Equip3Border.RectTransform, eventData.position, eventCamera))
        {
            slotIndex = 3;
            return true;
        }

        if (Contains(UI.Equip_Equip4Border.RectTransform, eventData.position, eventCamera))
        {
            slotIndex = 4;
            return true;
        }

        return false;
    }

    private bool Contains(RectTransform rectTransform, Vector2 screenPosition, Camera eventCamera)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera);
    }

    private Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return LoadManagedSprite(iconPath);
    }
}

public class CharacterUI_EquipSlotDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public int SlotIndex { get; private set; }
    public event Action<int, PointerEventData> DragStarted;
    public event Action<int, PointerEventData> Dragging;
    public event Action<int, PointerEventData> DragEnded;

    public void Initialize(int slotIndex)
    {
        SlotIndex = slotIndex;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        DragStarted?.Invoke(SlotIndex, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Dragging?.Invoke(SlotIndex, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DragEnded?.Invoke(SlotIndex, eventData);
    }
}
