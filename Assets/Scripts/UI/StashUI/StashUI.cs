using CrystalMagic.Core;

public class StashUI : UIBase<StashUIData>
{
    private readonly System.Collections.Generic.List<StashUI_InventoryItemView> _inventoryItemViews = new();
    private readonly System.Collections.Generic.List<StashUI_StashItemView> _stashItemViews = new();

    private CrystalMagic.UI.StashUIModel _model;
    private bool _isOpened;
    private bool _isModelEventSubscribed;

    public event System.Action AllCategoryRequested;
    public event System.Action SkillCategoryRequested;
    public event System.Action EquipCategoryRequested;
    public event System.Action PropsCategoryRequested;

    public void BindModel(CrystalMagic.UI.StashUIModel model)
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
        UI.ButtonList_All.ButtonPlus.onClick.AddListener(OnAllCategoryButton);
        UI.ButtonList_Skill.ButtonPlus.onClick.AddListener(OnSkillCategoryButton);
        UI.ButtonList_Equip.ButtonPlus.onClick.AddListener(OnEquipCategoryButton);
        UI.ButtonList_Props.ButtonPlus.onClick.AddListener(OnPropsCategoryButton);
        SubscribeModelEvents();

        if (_model != null)
            Render();
    }

    public override void OnClose()
    {
        UI.ButtonList_All.ButtonPlus.onClick.RemoveListener(OnAllCategoryButton);
        UI.ButtonList_Skill.ButtonPlus.onClick.RemoveListener(OnSkillCategoryButton);
        UI.ButtonList_Equip.ButtonPlus.onClick.RemoveListener(OnEquipCategoryButton);
        UI.ButtonList_Props.ButtonPlus.onClick.RemoveListener(OnPropsCategoryButton);
        UnsubscribeModelEvents();
        _isOpened = false;
    }

    private void Render()
    {
        if (_model == null)
            return;

        RenderInventory(_model.InventoryItems, _model.InventorySlotCount);
        RenderStash(_model.StashItems);
        UI.Coin_MoneyText.TextMeshProUGUI.text = _model.StashMoney.ToString();
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
        _inventoryItemViews.Clear();

        for (int i = 0; i < UI.InventoryView_Viewport_Content.GameObject.transform.childCount; i++)
        {
            StashUI_InventoryItemView itemView = UI.InventoryView_Viewport_Content.GameObject.transform.GetChild(i).GetComponent<StashUI_InventoryItemView>();
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
            UnityEngine.GameObject clone = Instantiate(UI.InventoryView_Viewport_Content_InventoryItem.GameObject, UI.InventoryView_Viewport_Content.GameObject.transform);
            clone.name = UI.InventoryView_Viewport_Content_InventoryItem.GameObject.name;
            clone.SetActive(true);

            StashUI_InventoryItemView itemView = clone.GetComponent<StashUI_InventoryItemView>();
            itemView.Rebind();
            _inventoryItemViews.Add(itemView);
        }
    }

    private void EnsureStashItemViews(int itemCount)
    {
        _stashItemViews.Clear();

        for (int i = 0; i < UI.StashView_Viewport_Content.GameObject.transform.childCount; i++)
        {
            StashUI_StashItemView itemView = UI.StashView_Viewport_Content.GameObject.transform.GetChild(i).GetComponent<StashUI_StashItemView>();
            itemView.Rebind();

            if (_stashItemViews.Count < itemCount)
            {
                itemView.gameObject.SetActive(true);
                _stashItemViews.Add(itemView);
            }
            else
            {
                itemView.gameObject.SetActive(false);
            }
        }

        while (_stashItemViews.Count < itemCount)
        {
            UnityEngine.GameObject clone = Instantiate(UI.StashView_Viewport_Content_StashItem.GameObject, UI.StashView_Viewport_Content.GameObject.transform);
            clone.name = UI.StashView_Viewport_Content_StashItem.GameObject.name;
            clone.SetActive(true);

            StashUI_StashItemView itemView = clone.GetComponent<StashUI_StashItemView>();
            itemView.Rebind();
            _stashItemViews.Add(itemView);
        }
    }

    private void OnModelChanged(CommonGameEvent gameEvent)
    {
        CrystalMagic.UI.StashUIModel eventModel = gameEvent.GetData<CrystalMagic.UI.StashUIModel>();
        if (eventModel != _model)
            return;

        Render();
    }

    private void RefreshCategorySelection()
    {
        SetCategorySelected(UI.ButtonList_All.GameObject, UI.ButtonList_All_Default.GameObject, UI.ButtonList_All_Select.GameObject, _model.Category == CrystalMagic.UI.StashCategory.All);
        SetCategorySelected(UI.ButtonList_Skill.GameObject, UI.ButtonList_Skill_Default.GameObject, UI.ButtonList_Skill_Select.GameObject, _model.Category == CrystalMagic.UI.StashCategory.Skill);
        SetCategorySelected(UI.ButtonList_Equip.GameObject, UI.ButtonList_Equip_Default.GameObject, UI.ButtonList_Equip_Select.GameObject, _model.Category == CrystalMagic.UI.StashCategory.Equip);
        SetCategorySelected(UI.ButtonList_Props.GameObject, UI.ButtonList_Props_Default.GameObject, UI.ButtonList_Props_Select.GameObject, _model.Category == CrystalMagic.UI.StashCategory.Props);
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

    private void SubscribeModelEvents()
    {
        if (_isModelEventSubscribed || _model == null)
            return;

        EventComponent.Instance.Subscribe(new CommonGameEvent(CrystalMagic.UI.StashUIModel.DataChangedEventName), OnModelChanged);
        _isModelEventSubscribed = true;
    }

    private void UnsubscribeModelEvents()
    {
        if (!_isModelEventSubscribed)
            return;

        EventComponent.Instance.Unsubscribe(new CommonGameEvent(CrystalMagic.UI.StashUIModel.DataChangedEventName), OnModelChanged);
        _isModelEventSubscribed = false;
    }
}
