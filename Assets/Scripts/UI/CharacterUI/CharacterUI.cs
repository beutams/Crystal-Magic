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
    private readonly CharacterUI_EquipSlotDragHandler[] _equipSlotHandlers = new CharacterUI_EquipSlotDragHandler[5];
    private readonly List<CrystalMagic.UI.CharacterSkillDisplayData> _currentSkillItems = new();
    private readonly CrystalMagic.UI.CharacterInventoryDisplayData[] _currentInventoryItems = new CrystalMagic.UI.CharacterInventoryDisplayData[32];
    private readonly CrystalMagic.UI.CharacterEquipDisplayData[] _currentEquipItems = new CrystalMagic.UI.CharacterEquipDisplayData[5];

    private bool _itemDragRaycastDisabled;
    private bool _skillDragRaycastDisabled;
    private CrystalMagic.UI.CharacterInventoryDisplayData _draggedInventoryItem;
    private CrystalMagic.UI.CharacterEquipDisplayData _draggedEquipItem;
    private CrystalMagic.UI.CharacterSkillDisplayData _draggedSkillItem;

    public event Action ChangeSkillRequested;
    public event Action<CrystalMagic.UI.CharacterInventoryDisplayData, int> InventorySkillStoneDropped;
    public event Action<CrystalMagic.UI.CharacterInventoryDisplayData, int> InventoryEquipDropped;
    public event Action<int> EquipReturnedToInventory;
    public event Action<int, int> BonusEquipSwapped;
    public event Action<CrystalMagic.UI.CharacterSkillDisplayData, int> SkillReordered;
    public event Action<CrystalMagic.UI.CharacterSkillDisplayData> SkillReturnedToInventory;

    public override void OnOpen()
    {
        UI.Skill_ChangeSkillBtn.ButtonPlus.onClick.AddListener(OnChangeSkillButton);
        EnsureEquipSlotHandlers();
        EnsureItemDragInitialized();
        EnsureSkillDragInitialized();
        SetItemDragVisible(false);
        SetSkillDragVisible(false);
        base.OnOpen();
    }

    public override void OnClose()
    {
        UI.Skill_ChangeSkillBtn.ButtonPlus.onClick.RemoveListener(OnChangeSkillButton);
        _draggedInventoryItem = null;
        _draggedEquipItem = null;
        _draggedSkillItem = null;
        SetItemDragVisible(false);
        SetSkillDragVisible(false);
        base.OnClose();
    }

    protected override void RefreshView()
    {
        if (Model == null)
            return;

        RenderSkill(Model.SkillItems);
        RenderInventory(Model.InventoryItems, Model.InventorySlotCount);
        RenderEquip(Model.EquipItems);
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

    private void RenderInventory(CrystalMagic.UI.CharacterInventoryDisplayData[] inventoryItems, int slotCount)
    {
        Array.Clear(_currentInventoryItems, 0, _currentInventoryItems.Length);
        EnsureInventoryItemViews(slotCount);

        for (int i = 0; i < _inventoryItemViews.Count; i++)
        {
            CrystalMagic.UI.CharacterInventoryDisplayData data = inventoryItems != null && i < inventoryItems.Length ? inventoryItems[i] : null;
            _currentInventoryItems[i] = data;
            _inventoryItemViews[i].Render(data);
        }
    }

    private void RenderEquip(CrystalMagic.UI.CharacterEquipDisplayData[] equipItems)
    {
        for (int i = 0; i < _currentEquipItems.Length; i++)
            _currentEquipItems[i] = equipItems != null && i < equipItems.Length ? equipItems[i] : null;

        RenderEquipSlot(UI.Equip_WeapenBorder_Weapen, _currentEquipItems[0]);
        RenderEquipSlot(UI.Equip_Equip1Border_Equip1, _currentEquipItems[1]);
        RenderEquipSlot(UI.Equip_Equip2Border_Equip2, _currentEquipItems[2]);
        RenderEquipSlot(UI.Equip_Equip3Border_Equip3, _currentEquipItems[3]);
        RenderEquipSlot(UI.Equip_Equip4Border_Equip4, _currentEquipItems[4]);
    }

    private void RenderEquipSlot(UINode node, CrystalMagic.UI.CharacterEquipDisplayData data)
    {
        Sprite icon = LoadIcon(data != null ? data.IconPath : string.Empty);
        node.Image.sprite = icon;
        node.Image.color = data != null ? Color.white : new Color(1f, 1f, 1f, 0.2f);
    }

    private void EnsureSkillItemViews(int itemCount)
    {
        _skillItemViews.Clear();

        for (int i = 0; i < UI.Skill_SkillChain_Viewport_Content.GameObject.transform.childCount; i++)
        {
            CharacterUI_SkillItemView itemView = UI.Skill_SkillChain_Viewport_Content.GameObject.transform.GetChild(i).GetComponent<CharacterUI_SkillItemView>();
            itemView.Rebind();
            BindSkillItemView(itemView);

            if (_skillItemViews.Count < itemCount)
            {
                itemView.gameObject.SetActive(true);
                _skillItemViews.Add(itemView);
            }
            else
            {
                itemView.gameObject.SetActive(false);
            }
        }

        while (_skillItemViews.Count < itemCount)
        {
            GameObject clone = Instantiate(UI.Skill_SkillChain_Viewport_Content_SkillItem.GameObject, UI.Skill_SkillChain_Viewport_Content.GameObject.transform);
            clone.name = UI.Skill_SkillChain_Viewport_Content_SkillItem.GameObject.name;
            clone.SetActive(true);

            CharacterUI_SkillItemView itemView = clone.GetComponent<CharacterUI_SkillItemView>();
            itemView.Rebind();
            BindSkillItemView(itemView);
            _skillItemViews.Add(itemView);
        }
    }

    private void EnsureInventoryItemViews(int itemCount)
    {
        _inventoryItemViews.Clear();

        for (int i = 0; i < UI.InventoryView_Viewport_Content.GameObject.transform.childCount; i++)
        {
            CharacterUI_InventoryItemView itemView = UI.InventoryView_Viewport_Content.GameObject.transform.GetChild(i).GetComponent<CharacterUI_InventoryItemView>();
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

            CharacterUI_InventoryItemView itemView = clone.GetComponent<CharacterUI_InventoryItemView>();
            itemView.Rebind();
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
        itemView.DragStarted += HandleSkillDragStarted;
        itemView.Dragging += HandleSkillDragging;
        itemView.DragEnded += HandleSkillDragEnded;
    }

    private void EnsureEquipSlotHandlers()
    {
        BindEquipSlotHandler(0, UI.Equip_WeapenBorder.GameObject);
        BindEquipSlotHandler(1, UI.Equip_Equip1Border.GameObject);
        BindEquipSlotHandler(2, UI.Equip_Equip2Border.GameObject);
        BindEquipSlotHandler(3, UI.Equip_Equip3Border.GameObject);
        BindEquipSlotHandler(4, UI.Equip_Equip4Border.GameObject);
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
        _equipSlotHandlers[slotIndex] = handler;
    }

    private void HandleInventoryDragStarted(CrystalMagic.UI.CharacterInventoryDisplayData data, PointerEventData eventData)
    {
        if (data == null || eventData == null)
            return;

        _draggedEquipItem = null;
        _draggedSkillItem = null;
        _draggedInventoryItem = data;
        UI.ItemDrag_Icon.Image.sprite = LoadIcon(data.IconPath);
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

        _draggedInventoryItem = null;
        SetItemDragVisible(false);
    }

    private void HandleEquipDragStarted(int slotIndex, PointerEventData eventData)
    {
        if (eventData == null || slotIndex < 0 || slotIndex >= _currentEquipItems.Length)
            return;

        CrystalMagic.UI.CharacterEquipDisplayData data = _currentEquipItems[slotIndex];
        if (data == null || data.ItemId <= 0 || data.ItemType == CrystalMagic.Game.Data.ItemType.None)
            return;

        _draggedInventoryItem = null;
        _draggedSkillItem = null;
        _draggedEquipItem = data;
        UI.ItemDrag_Icon.Image.sprite = LoadIcon(data.IconPath);
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
        bool shouldSwapBonusSlot = _draggedEquipItem != null
            && _draggedEquipItem.SlotIndex == slotIndex
            && TryGetHoveredEquipSlotIndex(eventData, out hoveredSlotIndex)
            && slotIndex >= 1
            && slotIndex <= 4
            && hoveredSlotIndex >= 1
            && hoveredSlotIndex <= 4
            && hoveredSlotIndex != slotIndex;

        bool shouldReturnToInventory = _draggedEquipItem != null
            && _draggedEquipItem.SlotIndex == slotIndex
            && IsPointerOverInventory(eventData);

        _draggedEquipItem = null;
        SetItemDragVisible(false);

        if (shouldSwapBonusSlot)
            BonusEquipSwapped?.Invoke(slotIndex, hoveredSlotIndex);

        if (shouldReturnToInventory)
            EquipReturnedToInventory?.Invoke(slotIndex);
    }

    private void HandleSkillDragStarted(CrystalMagic.UI.CharacterSkillDisplayData data, PointerEventData eventData)
    {
        if (data == null || eventData == null)
            return;

        _draggedInventoryItem = null;
        _draggedEquipItem = null;
        _draggedSkillItem = data;
        UI.SkillDrag_Skill.Image.sprite = LoadIcon(data.SkillIconPath);
        UI.SkillDrag_Effect_EffectIcon.Image.sprite = LoadIcon(data.EffectIconPath);
        UI.SkillDrag_Index_IndexNum.TextMeshProUGUI.text = data.DisplayIndex.ToString();
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
        else if (IsPointerOverInventory(eventData))
        {
            SkillReturnedToInventory?.Invoke(data);
        }

        _draggedSkillItem = null;
        SetSkillDragVisible(false);
    }

    private void EnsureItemDragInitialized()
    {
        if (_itemDragRaycastDisabled || UI.ItemDrag.GameObject == null)
            return;

        Graphic[] graphics = UI.ItemDrag.GameObject.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;

        _itemDragRaycastDisabled = true;
    }

    private void EnsureSkillDragInitialized()
    {
        if (_skillDragRaycastDisabled || UI.SkillDrag.GameObject == null)
            return;

        Graphic[] graphics = UI.SkillDrag.GameObject.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
            graphics[i].raycastTarget = false;

        _skillDragRaycastDisabled = true;
    }

    private void SetItemDragVisible(bool visible)
    {
        if (UI.ItemDrag.GameObject == null)
            return;

        if (UI.ItemDrag.GameObject.activeSelf != visible)
            UI.ItemDrag.GameObject.SetActive(visible);
    }

    private void SetSkillDragVisible(bool visible)
    {
        if (UI.SkillDrag.GameObject == null)
            return;

        if (UI.SkillDrag.GameObject.activeSelf != visible)
            UI.SkillDrag.GameObject.SetActive(visible);
    }

    private void UpdateItemDragPosition(PointerEventData eventData)
    {
        if (eventData == null || UI.ItemDrag.RectTransform == null)
            return;

        RectTransform parentRect = UI.ItemDrag.RectTransform.parent as RectTransform;
        if (parentRect == null)
        {
            UI.ItemDrag.RectTransform.position = eventData.position;
            return;
        }

        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventCamera, out Vector2 localPoint))
            UI.ItemDrag.RectTransform.anchoredPosition = localPoint;
    }

    private void UpdateSkillDragPosition(PointerEventData eventData)
    {
        if (eventData == null || UI.SkillDrag.RectTransform == null)
            return;

        RectTransform parentRect = UI.SkillDrag.RectTransform.parent as RectTransform;
        if (parentRect == null)
        {
            UI.SkillDrag.RectTransform.position = eventData.position;
            return;
        }

        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventCamera, out Vector2 localPoint))
            UI.SkillDrag.RectTransform.anchoredPosition = localPoint;
    }

    private bool IsPointerOverSkillChain(PointerEventData eventData)
    {
        if (eventData == null || UI.Skill_SkillChain.RectTransform == null)
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

    private bool IsPointerOverInventory(PointerEventData eventData)
    {
        if (eventData == null || UI.InventoryView.RectTransform == null)
            return false;

        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        return RectTransformUtility.RectangleContainsScreenPoint(UI.InventoryView.RectTransform, eventData.position, eventCamera);
    }

    private bool TryGetHoveredEquipSlotIndex(PointerEventData eventData, out int slotIndex)
    {
        slotIndex = -1;
        if (eventData == null)
            return false;

        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;
        if (Contains(UI.Equip_WeapenBorder.RectTransform, eventData.position, eventCamera))
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
        return rectTransform != null && RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera);
    }

    private void OnChangeSkillButton()
    {
        ChangeSkillRequested?.Invoke();
    }

    private Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return ResourceComponent.Instance.Load<Sprite>(iconPath);
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
