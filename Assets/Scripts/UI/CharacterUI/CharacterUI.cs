using CrystalMagic.Core;
using CrystalMagic.UI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CharacterUI : UIBase<CharacterUIData, CrystalMagic.UI.CharacterUIModel>
{
    private readonly List<CharacterUI_SkillItemView> _skillItemViews = new();
    private readonly List<CharacterUI_InventoryItemView> _inventoryItemViews = new();
    private readonly List<CharacterUI_InventoryItemView> _skillInventoryItemViews = new();
    private readonly float[] _bookmarkWidths = new float[4];
    private Sprite _selectedChainSprite;
    private Sprite _idleChainSprite;
    private Color _selectedChainTextColor;
    private Color _idleChainTextColor;
    private CharacterPage _renderedPage = (CharacterPage)(-1);
    private int _renderedChain = -1;
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
    public event Action<CharacterPage> PageRequested;
    public event Action<int> ChainRequested;
    public event Action InventorySortRequested;

    public CharacterUI_SettingView SettingsView => UI.Setting.GameObject.GetComponent<CharacterUI_SettingView>();
    public bool IsSettingsPage => Model != null && Model.SelectedPage == CharacterPage.Setting;

    public void ShowSettings() => PageRequested?.Invoke(CharacterPage.Setting);

    // The current prefab keeps a drag visual inside each page, not at the book root.
    private UINode ActiveItemDrag => UI.Skill.GameObject.activeInHierarchy ? UI.Skill_SkillDrag : UI.Equip_ItemDrag;
    private UINode ActiveItemDragIcon => UI.Skill.GameObject.activeInHierarchy ? UI.Skill_SkillDrag_Mask_Icon : UI.Equip_ItemDrag_Mask_Icon;

    protected override void OnInit()
    {
        base.OnInit();
        SettingsView.InitializeBindings();
        UI.Equip_InventorySort.ButtonPlus.onClick.AddListener(HandleInventorySort);
        UI.Skill_InventorySort.ButtonPlus.onClick.AddListener(HandleInventorySort);
        for (int i = 0; i < 4; i++)
        {
            int index = i;
            UINode bookmark = GetBookmark(i);
            _bookmarkWidths[i] = bookmark.RectTransform.sizeDelta.x;
            bookmark.ButtonPlus.onClick.AddListener(() => PageRequested?.Invoke((CharacterPage)index));
        }
        for (int i = 0; i < 5; i++)
        {
            int index = i;
            GetChainTab(i).ButtonPlus.onClick.AddListener(() => ChainRequested?.Invoke(index));
        }
        _selectedChainSprite = UI.Skill_ChainTabs_Chain1.Image.sprite;
        _idleChainSprite = UI.Skill_ChainTabs_Chain2.Image.sprite;
        _selectedChainTextColor = UI.Skill_ChainTabs_Chain1_Label.TextMeshProUGUI.color;
        _idleChainTextColor = UI.Skill_ChainTabs_Chain2_Label.TextMeshProUGUI.color;
    }

    private UINode GetBookmark(int index) => index switch
    {
        0 => UI.Buttons_Equip, 1 => UI.Buttons_Skill,
        2 => UI.Buttons_Handbook, _ => UI.Buttons_Setting,
    };

    private UINode GetChainTab(int index) => index switch
    {
        0 => UI.Skill_ChainTabs_Chain1, 1 => UI.Skill_ChainTabs_Chain2,
        2 => UI.Skill_ChainTabs_Chain3, 3 => UI.Skill_ChainTabs_Chain4,
        _ => UI.Skill_ChainTabs_Chain5,
    };

    private UINode GetEquipSelect(int index) => index switch
    {
        0 => UI.Equip_MagicStoneBorder_Select, 1 => UI.Equip_Equip1Border_Select,
        2 => UI.Equip_Equip2Border_Select, 3 => UI.Equip_Equip3Border_Select,
        _ => UI.Equip_Equip4Border_Select,
    };

    private UINode GetChainLabel(int index) => index switch
    {
        0 => UI.Skill_ChainTabs_Chain1_Label, 1 => UI.Skill_ChainTabs_Chain2_Label,
        2 => UI.Skill_ChainTabs_Chain3_Label, 3 => UI.Skill_ChainTabs_Chain4_Label,
        _ => UI.Skill_ChainTabs_Chain5_Label,
    };

    private UINode GetPropSelect(int index) => index switch
    {
        0 => UI.Equip_PropSlot1_Select, 1 => UI.Equip_PropSlot2_Select,
        _ => UI.Equip_PropSlot3_Select,
    };

    private void RenderNavigation()
    {
        if (_renderedPage != Model.SelectedPage || _renderedChain != Model.SelectedChainIndex)
        {
            CancelDrag();
            ClearSlotSelection();
            UI.Skill_SkillChain.GameObject.GetComponent<ScrollRect>().verticalNormalizedPosition = 1f;
            _renderedPage = Model.SelectedPage;
            _renderedChain = Model.SelectedChainIndex;
        }
        UI.Equip.GameObject.SetActive(Model.SelectedPage == CharacterPage.Equip);
        UI.Skill.GameObject.SetActive(Model.SelectedPage == CharacterPage.Skill);
        UI.Handbook.GameObject.SetActive(Model.SelectedPage == CharacterPage.Handbook);
        UI.Setting.GameObject.SetActive(Model.SelectedPage == CharacterPage.Setting);
        for (int i = 0; i < 4; i++)
        {
            RectTransform rect = GetBookmark(i).RectTransform;
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                _bookmarkWidths[i] + (i == (int)Model.SelectedPage ? 50f : 0f));
        }
        for (int i = 0; i < 5; i++)
        {
            GetChainTab(i).Image.sprite = i == Model.SelectedChainIndex ? _selectedChainSprite : _idleChainSprite;
            GetChainLabel(i).TextMeshProUGUI.color = i == Model.SelectedChainIndex ? _selectedChainTextColor : _idleChainTextColor;
        }
    }

    private void CancelDrag()
    {
        _draggedInventoryItem = null;
        _draggedEquipItem = null;
        _draggedSkillItem = null;
        _draggedPropItem = null;
        SetDragVisible(UI.Equip_ItemDrag.GameObject, false);
        SetDragVisible(UI.Skill_SkillDrag.GameObject, false);
    }

    private void HandleInventorySort()
    {
        CancelDrag();
        ClearSlotSelection();
        InventorySortRequested?.Invoke();
    }

    private void ClearSlotSelection()
    {
        for (int i = 0; i < 5; i++) GetEquipSelect(i).GameObject.SetActive(false);
        for (int i = 0; i < 3; i++) GetPropSelect(i).GameObject.SetActive(false);
    }

    private void HandleEquipHover(int index, bool hovered) => GetEquipSelect(index).GameObject.SetActive(hovered);
    private void HandlePropHover(int index, bool hovered) => GetPropSelect(index).GameObject.SetActive(hovered);

    public override void OnOpen()
    {
        _renderedPage = (CharacterPage)(-1);
        _renderedChain = -1;
        EnsureEquipSlotHandlers();
        EnsurePropSlotHandlers();
        EnsureItemDragInitialized();
        EnsureSkillDragInitialized();
        UI.Equip_ItemDrag.GameObject.transform.SetAsLastSibling();
        UI.Skill_SkillDrag.GameObject.transform.SetAsLastSibling();
        SetItemDragVisible(false);
        SetSkillDragVisible(false);
        base.OnOpen();
    }

    public override void OnClose()
    {
        CancelDrag();
        ClearSlotSelection();
        UISubViewBase.ReleaseAllToPool(_skillItemViews);
        UISubViewBase.ReleaseAllToPool(_inventoryItemViews);
        UISubViewBase.ReleaseAllToPool(_skillInventoryItemViews);
        base.OnClose();
    }

    protected override void RefreshView()
    {
        if (Model == null)
            return;

        CancelDrag();
        RenderNavigation();
        if (Model.SelectedPage == CharacterPage.Setting)
        {
            SettingsView.Render(Model.Settings, Model.SelectedSettingsSection);
            return;
        }
        RenderSkill(Model.SkillItems);
        RenderInventory(Model.InventoryItems, Model.InventorySlotCount, _inventoryItemViews,
            UI.Equip_InventoryView_InventoryItem, UI.Equip_InventoryView);
        RenderInventory(Model.InventoryItems, Model.InventorySlotCount, _skillInventoryItemViews,
            UI.Skill_InventoryView_InventoryItem, UI.Skill_InventoryView);
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

    private void RenderInventory(IReadOnlyList<CrystalMagic.UI.CharacterInventoryDisplayData> inventoryItems, int slotCount,
        List<CharacterUI_InventoryItemView> views, UINode template, UINode container)
    {
        EnsureInventoryItemViews(slotCount, views, template, container);

        for (int i = 0; i < views.Count; i++)
        {
            CrystalMagic.UI.CharacterInventoryDisplayData data = inventoryItems != null && i < inventoryItems.Count ? inventoryItems[i] : null;
            views[i].Render(data, i);
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
        UI.Equip_MagicStoneBorder_Default.GameObject.SetActive(_currentEquipItems[0] == null);
        UI.Equip_Equip1Border_Default.GameObject.SetActive(_currentEquipItems[1] == null);
        UI.Equip_Equip2Border_Default.GameObject.SetActive(_currentEquipItems[2] == null);
        UI.Equip_Equip3Border_Default.GameObject.SetActive(_currentEquipItems[3] == null);
        UI.Equip_Equip4Border_Default.GameObject.SetActive(_currentEquipItems[4] == null);
    }

    private void RenderProps(CrystalMagic.UI.CharacterPropDisplayData[] propItems)
    {
        for (int i = 0; i < _currentPropItems.Length; i++)
            _currentPropItems[i] = propItems != null && i < propItems.Length ? propItems[i] : null;

        RenderPropSlot(UI.Equip_PropSlot1_Icon, UI.Equip_PropSlot1_Count, _currentPropItems[0]);
        RenderPropSlot(UI.Equip_PropSlot2_Icon, UI.Equip_PropSlot2_Count, _currentPropItems[1]);
        RenderPropSlot(UI.Equip_PropSlot3_Icon, UI.Equip_PropSlot3_Count, _currentPropItems[2]);
        UI.Equip_PropSlot1_Default.GameObject.SetActive(_currentPropItems[0] == null);
        UI.Equip_PropSlot2_Default.GameObject.SetActive(_currentPropItems[1] == null);
        UI.Equip_PropSlot3_Default.GameObject.SetActive(_currentPropItems[2] == null);
    }

    private void RenderPropSlot(UINode iconNode, UINode countNode, CrystalMagic.UI.CharacterPropDisplayData data)
    {
        iconNode.Image.sprite = LoadIcon(data != null ? data.IconPath : string.Empty);
        iconNode.GameObject.SetActive(iconNode.Image.sprite != null);
        iconNode.Image.color = Color.white;
        countNode.TextMeshProUGUI.text = data != null && data.Count > 0 ? data.Count.ToString() : string.Empty;
    }

    private void RenderEquipSlot(UINode node, CrystalMagic.UI.CharacterEquipDisplayData data)
    {
        Sprite icon = LoadIcon(data != null ? data.IconPath : string.Empty);
        node.Image.sprite = icon;
        node.GameObject.SetActive(icon != null);
        node.Image.color = Color.white;
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

    private void EnsureInventoryItemViews(int itemCount, List<CharacterUI_InventoryItemView> views,
        UINode template, UINode container)
    {
        template.GameObject.SetActive(false);

        while (views.Count > itemCount)
        {
            int lastIndex = views.Count - 1;
            CharacterUI_InventoryItemView itemView = views[lastIndex];
            UISubViewBase.ReleaseToPool(itemView);
            views.RemoveAt(lastIndex);
        }

        CharacterUI_InventoryItemView templateView = template.GameObject.GetComponent<CharacterUI_InventoryItemView>();
        if (templateView == null)
            return;

        UISubViewBase.EnsurePoolCapacity(templateView, itemCount, itemCount);

        while (views.Count < itemCount)
        {
            CharacterUI_InventoryItemView itemView = UISubViewBase.AcquireFromPool(templateView, container.GameObject.transform);
            if (itemView == null)
                break;

            BindInventoryItemView(itemView);
            views.Add(itemView);
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
        BindPropSlotHandler(0, UI.Equip_PropSlot1.GameObject);
        BindPropSlotHandler(1, UI.Equip_PropSlot2.GameObject);
        BindPropSlotHandler(2, UI.Equip_PropSlot3.GameObject);
    }

    private void BindPropSlotHandler(int slotIndex, GameObject target)
    {
        CharacterUI_PropSlotDragHandler handler = target.GetComponent<CharacterUI_PropSlotDragHandler>();
        handler.Initialize(slotIndex);
        handler.HoverChanged -= HandlePropHover;
        handler.HoverChanged += HandlePropHover;
        handler.DragStarted -= HandlePropDragStarted;
        handler.Dragging -= HandlePropDragging;
        handler.DragEnded -= HandlePropDragEnded;
        handler.DragStarted += HandlePropDragStarted;
        handler.Dragging += HandlePropDragging;
        handler.DragEnded += HandlePropDragEnded;
    }

    private void BindEquipSlotHandler(int slotIndex, GameObject target)
    {
        CharacterUI_EquipSlotDragHandler handler = target.GetComponent<CharacterUI_EquipSlotDragHandler>();
        handler.Initialize(slotIndex);
        handler.HoverChanged -= HandleEquipHover;
        handler.HoverChanged += HandleEquipHover;
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
        SetDragIcon(ActiveItemDragIcon, data.IconPath);
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
        SetDragIcon(ActiveItemDragIcon, data.IconPath);
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
        SetDragIcon(UI.Skill_SkillDrag_Mask_Icon, data.SkillIconPath);
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
        SetDragIcon(ActiveItemDragIcon, data.IconPath);
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
        DisableDragRaycasts(UI.Equip_ItemDrag.GameObject, ref _itemDragRaycastDisabled);
    }

    private void EnsureSkillDragInitialized()
    {
        DisableDragRaycasts(UI.Skill_SkillDrag.GameObject, ref _skillDragRaycastDisabled);
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
        SetDragVisible(ActiveItemDrag.GameObject, visible);
    }

    private void SetSkillDragVisible(bool visible)
    {
        SetDragVisible(UI.Skill_SkillDrag.GameObject, visible);
    }

    private static void SetDragVisible(GameObject dragObject, bool visible)
    {
        if (dragObject.activeSelf != visible)
            dragObject.SetActive(visible);
    }

    private void UpdateItemDragPosition(PointerEventData eventData)
    {
        UpdateDragPosition(ActiveItemDrag.RectTransform, eventData);
    }

    private void UpdateSkillDragPosition(PointerEventData eventData)
    {
        UpdateDragPosition(UI.Skill_SkillDrag.RectTransform, eventData);
    }

    private void SetDragIcon(UINode node, string path)
    {
        node.Image.sprite = LoadIcon(path);
        node.GameObject.SetActive(node.Image.sprite != null);
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
        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parentRect, eventData.position, eventCamera, out Vector3 worldPoint))
            dragRect.position = worldPoint;
    }

    private bool IsPointerOverSkillChain(PointerEventData eventData)
    {
        if (eventData == null || !UI.Skill.GameObject.activeInHierarchy)
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

        float pointerY = eventData.position.y;
        Camera eventCamera = eventData.pressEventCamera != null ? eventData.pressEventCamera : eventData.enterEventCamera;

        for (int i = 0; i < _skillItemViews.Count; i++)
        {
            RectTransform rectTransform = _skillItemViews[i].transform as RectTransform;
            if (rectTransform == null || !_skillItemViews[i].gameObject.activeInHierarchy)
                continue;

            Vector3[] corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            float bottom = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]).y;
            float top = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[1]).y;
            float mid = (bottom + top) * 0.5f;

            if (pointerY > mid)
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
        if (itemView == null || !itemView.gameObject.activeInHierarchy || !itemView.transform.IsChildOf(transform))
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
        if (handler == null || !handler.gameObject.activeInHierarchy || !handler.transform.IsChildOf(UI.Equip.GameObject.transform))
            return false;

        slotIndex = handler.SlotIndex;
        return slotIndex >= 0;
    }

    private bool TryGetHoveredEquipSlotIndex(PointerEventData eventData, out int slotIndex)
    {
        slotIndex = -1;
        if (eventData == null || !UI.Equip.GameObject.activeInHierarchy)
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
