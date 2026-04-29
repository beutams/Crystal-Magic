using CrystalMagic.Core;

public class StashUI : UIBase<StashUIData, CrystalMagic.UI.StashUIModel>
{
    private readonly System.Collections.Generic.List<StashUI_InventoryItemView> _inventoryItemViews = new();
    private readonly System.Collections.Generic.List<StashUI_StashItemView> _stashItemViews = new();

    public event System.Action AllCategoryRequested;
    public event System.Action SkillCategoryRequested;
    public event System.Action EquipCategoryRequested;
    public event System.Action PropsCategoryRequested;

    public override void OnOpen()
    {
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

            _stashItemViews.Add(itemView);
        }
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

}
