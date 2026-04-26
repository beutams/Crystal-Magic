using CrystalMagic.Core;

public class CharacterUI : UIBase<CharacterUIData>
{
    private readonly System.Collections.Generic.List<CharacterUI_SkillItemView> _skillItemViews = new();
    private readonly System.Collections.Generic.List<CharacterUI_InventoryItemView> _inventoryItemViews = new();

    private CrystalMagic.UI.CharacterUIModel _model;
    private bool _isOpened;
    private bool _isModelEventSubscribed;
    public event System.Action ChangeSkillRequested;

    public void BindModel(CrystalMagic.UI.CharacterUIModel model)
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
        UI.Skill_ChangeSkillBtn.ButtonPlus.onClick.AddListener(OnChangeSkillButton);
        SubscribeModelEvents();

        if (_model != null)
            Render();
    }

    public override void OnClose()
    {
        UI.Skill_ChangeSkillBtn.ButtonPlus.onClick.RemoveListener(OnChangeSkillButton);
        UnsubscribeModelEvents();
        _isOpened = false;
    }

    private void Render()
    {
        if (_model == null)
            return;

        RenderSkill(_model.SkillItems);
        RenderInventory(_model.InventoryItems, _model.InventorySlotCount);
        RenderEquip(_model.EquipItems);
    }

    private void RenderSkill(System.Collections.Generic.IReadOnlyList<CrystalMagic.UI.CharacterSkillDisplayData> skillItems)
    {
        int skillItemCount = skillItems != null ? skillItems.Count : 0;
        EnsureSkillItemViews(skillItemCount);

        for (int i = 0; i < _skillItemViews.Count; i++)
        {
            CrystalMagic.UI.CharacterSkillDisplayData data = skillItems != null && i < skillItems.Count ? skillItems[i] : null;
            _skillItemViews[i].Render(data);
        }
    }

    private void RenderInventory(CrystalMagic.UI.CharacterInventoryDisplayData[] inventoryItems, int slotCount)
    {
        EnsureInventoryItemViews(slotCount);

        for (int i = 0; i < _inventoryItemViews.Count; i++)
        {
            CrystalMagic.UI.CharacterInventoryDisplayData data = inventoryItems != null && i < inventoryItems.Length ? inventoryItems[i] : null;
            _inventoryItemViews[i].Render(data);
        }
    }

    private void RenderEquip(CrystalMagic.UI.CharacterEquipDisplayData[] equipItems)
    {
        RenderEquipSlot(UI.Equip_Weapen, equipItems != null && equipItems.Length > 0 ? equipItems[0] : null);
        RenderEquipSlot(UI.Equip_Equip1, equipItems != null && equipItems.Length > 1 ? equipItems[1] : null);
        RenderEquipSlot(UI.Equip_Equip2, equipItems != null && equipItems.Length > 2 ? equipItems[2] : null);
        RenderEquipSlot(UI.Equip_Equip3, equipItems != null && equipItems.Length > 3 ? equipItems[3] : null);
        RenderEquipSlot(UI.Equip_Equip4, equipItems != null && equipItems.Length > 4 ? equipItems[4] : null);
    }

    private void RenderEquipSlot(UINode node, CrystalMagic.UI.CharacterEquipDisplayData data)
    {
        UnityEngine.Sprite icon = LoadIcon(data != null ? data.IconPath : string.Empty);
        node.Image.sprite = icon;
        node.Image.color = data != null ? UnityEngine.Color.white : new UnityEngine.Color(1f, 1f, 1f, 0.2f);
    }

    private void EnsureSkillItemViews(int itemCount)
    {
        _skillItemViews.Clear();

        for (int i = 0; i < UI.Skill_SkillChain_Viewport_Content.GameObject.transform.childCount; i++)
        {
            CharacterUI_SkillItemView itemView = UI.Skill_SkillChain_Viewport_Content.GameObject.transform.GetChild(i).GetComponent<CharacterUI_SkillItemView>();
            itemView.Rebind();

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
            UnityEngine.GameObject clone = Instantiate(UI.Skill_SkillChain_Viewport_Content_SkillItem.GameObject, UI.Skill_SkillChain_Viewport_Content.GameObject.transform);
            clone.name = UI.Skill_SkillChain_Viewport_Content_SkillItem.GameObject.name;
            clone.SetActive(true);

            CharacterUI_SkillItemView itemView = clone.GetComponent<CharacterUI_SkillItemView>();
            itemView.Rebind();
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

            CharacterUI_InventoryItemView itemView = clone.GetComponent<CharacterUI_InventoryItemView>();
            itemView.Rebind();
            _inventoryItemViews.Add(itemView);
        }
    }

    private void OnModelChanged(CommonGameEvent gameEvent)
    {
        CrystalMagic.UI.CharacterUIModel eventModel = gameEvent.GetData<CrystalMagic.UI.CharacterUIModel>();
        if (eventModel != _model)
            return;

        Render();
    }

    private void OnChangeSkillButton()
    {
        ChangeSkillRequested?.Invoke();
    }

    private void SubscribeModelEvents()
    {
        if (_isModelEventSubscribed || _model == null)
            return;

        EventComponent.Instance.Subscribe(new CommonGameEvent(CrystalMagic.UI.CharacterUIModel.DataChangedEventName), OnModelChanged);
        _isModelEventSubscribed = true;
    }

    private void UnsubscribeModelEvents()
    {
        if (!_isModelEventSubscribed)
            return;

        EventComponent.Instance.Unsubscribe(new CommonGameEvent(CrystalMagic.UI.CharacterUIModel.DataChangedEventName), OnModelChanged);
        _isModelEventSubscribed = false;
    }

    private UnityEngine.Sprite LoadIcon(string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath))
            return null;

        return ResourceComponent.Instance.Load<UnityEngine.Sprite>(iconPath);
    }
}
