using CrystalMagic.Core;
using CrystalMagic.Game.Data;

namespace CrystalMagic.UI
{
    public sealed class CharacterUIController : UIControllerBase<CharacterUI, CharacterUIModel>, IUIOpenDataReceiver<CharacterPage>
    {
        private EffectSelectUI _effectSelectUI;
        private bool _settingsPendingSave;
        private CharacterPage _initialPage = CharacterPage.Equip;
        private readonly System.Action<CrystalMagic.Core.CommonGameEvent> _refreshHandler;

        public CharacterUIController(CharacterUI view, CharacterUIModel model)
            : base(view, model)
        {
            _refreshHandler = _ => Model.Refresh();
        }

        public void SetOpenData(CharacterPage page) => _initialPage = page;

        protected override void OnOpen()
        {
            Model.ResetNavigation(_initialPage);
            _initialPage = CharacterPage.Equip;
            View.BindModel(Model);
            Bindings.Bind(() => View.PageRequested += OnPageRequested, () => View.PageRequested -= OnPageRequested);
            Bindings.Bind(() => View.ChainRequested += OnChainRequested, () => View.ChainRequested -= OnChainRequested);
            Bindings.Bind(() => View.InventorySortRequested += OnInventorySortRequested, () => View.InventorySortRequested -= OnInventorySortRequested);
            Bindings.Bind(() => View.SettingsView.SectionRequested += OnSettingsSectionRequested, () => View.SettingsView.SectionRequested -= OnSettingsSectionRequested);
            Bindings.Bind(() => View.SettingsView.ValueChanged += OnSettingValueChanged, () => View.SettingsView.ValueChanged -= OnSettingValueChanged);
            Bindings.Bind(() => View.SettingsView.LanguageRequested += OnLanguageRequested, () => View.SettingsView.LanguageRequested -= OnLanguageRequested);
            Bindings.Bind(() => View.SettingsView.EditCompleted += FlushSettings, () => View.SettingsView.EditCompleted -= FlushSettings);
            Bindings.Bind(() => View.SettingsView.SaveRequested += OnSaveRequested, () => View.SettingsView.SaveRequested -= OnSaveRequested);
            Bindings.Bind(() => View.SettingsView.ReturnMainMenuRequested += OnReturnMainMenuRequested, () => View.SettingsView.ReturnMainMenuRequested -= OnReturnMainMenuRequested);
            BindEvent(new CommonGameEvent(GameSettingsComponent.SettingsChangedEventName), OnSettingsChanged);
            Model.SetSettings(GameSettingsComponent.Instance.GetSettingsCopy());
            View.InventorySkillStoneDropped += OnInventorySkillStoneDropped;
            View.InventoryEquipDropped += OnInventoryEquipDropped;
            View.InventoryItemMoved += OnInventoryItemMoved;
            View.InventoryPropDropped += OnInventoryPropDropped;
            View.EquipReturnedToInventory += OnEquipReturnedToInventory;
            View.SpiritEquipSwapped += OnSpiritEquipSwapped;
            View.SkillAdditionRequested += OnSkillAdditionRequested;
            View.SkillReordered += OnSkillReordered;
            View.SkillReturnedToInventory += OnSkillReturnedToInventory;
            View.PropReturnedToInventory += OnPropReturnedToInventory;
            View.PropSlotMoved += OnPropSlotMoved;
            BindEvent(new CommonGameEvent(SaveDataComponent.CharacterDataChangedEventName), _refreshHandler);
            Model.Refresh();
        }

        protected override void OnClose()
        {
            FlushSettings();
            View.InventorySkillStoneDropped -= OnInventorySkillStoneDropped;
            View.InventoryEquipDropped -= OnInventoryEquipDropped;
            View.InventoryItemMoved -= OnInventoryItemMoved;
            View.InventoryPropDropped -= OnInventoryPropDropped;
            View.EquipReturnedToInventory -= OnEquipReturnedToInventory;
            View.SpiritEquipSwapped -= OnSpiritEquipSwapped;
            View.SkillAdditionRequested -= OnSkillAdditionRequested;
            View.SkillReordered -= OnSkillReordered;
            View.SkillReturnedToInventory -= OnSkillReturnedToInventory;
            View.PropReturnedToInventory -= OnPropReturnedToInventory;
            View.PropSlotMoved -= OnPropSlotMoved;
            CloseEffectSelectUI();
        }

        private void OnPageRequested(CharacterPage page)
        {
            if (page != CharacterPage.Setting &&
                GameRuntimeStateUtility.TryGetPlayerEntity(out Unity.Entities.EntityManager entityManager, out Unity.Entities.Entity player) &&
                PlayerCharacterUtility.IsEditLocked(entityManager, player))
                return;

            CloseEffectSelectUI();
            if (Model.SelectedPage == CharacterPage.Setting)
                FlushSettings();
            Model.SelectPage(page);
        }

        private void OnChainRequested(int index)
        {
            CloseEffectSelectUI();
            Model.SelectChain(index);
        }

        private void OnSettingsSectionRequested(CharacterSettingsSection section)
        {
            FlushSettings();
            Model.SelectSettingsSection(section);
        }

        private void OnSettingsChanged(CommonGameEvent gameEvent)
        {
            Model.SetSettings(gameEvent.GetData<GameSettingsData>());
        }

        private void OnSettingValueChanged(CharacterSettingValue field, float value)
        {
            GameSettingsData settings = GameSettingsComponent.Instance.GetSettingsCopy();
            value = UnityEngine.Mathf.Clamp01(value);
            switch (field)
            {
                case CharacterSettingValue.MasterVolume: settings.MasterVolume = value; break;
                case CharacterSettingValue.BgmVolume: settings.BgmVolume = value; break;
                case CharacterSettingValue.SfxVolume: settings.SfxVolume = value; break;
                default: return;
            }
            _settingsPendingSave = true;
            GameSettingsComponent.Instance.SetSettings(settings);
        }

        private void OnLanguageRequested(GameLanguage language)
        {
            GameSettingsData settings = GameSettingsComponent.Instance.GetSettingsCopy();
            settings.Language = language;
            _settingsPendingSave = true;
            GameSettingsComponent.Instance.SetSettings(settings);
            FlushSettings();
        }

        private void FlushSettings()
        {
            if (_settingsPendingSave && GameSettingsComponent.Instance.SaveSettings())
                _settingsPendingSave = false;
        }

        private void OnSaveRequested()
        {
            UIComponent.Instance.OpenChild<ConfirmUI>(View, new ConfirmUIOpenData(
                LocalizationComponent.Instance.Get("ui.confirm.save"),
                LocalizationComponent.Instance.Get("ui.confirm.save_current.content"),
                ConfirmSave));
        }

        private void ConfirmSave()
        {
            FlushSettings();
            if (!SaveDataComponent.Instance.Save())
                return;

            UIComponent.Instance.OpenChild<ConfirmSingleUI>(View, new ConfirmUIOpenData(
                LocalizationComponent.Instance.Get("ui.confirm.save"),
                LocalizationComponent.Instance.Get("ui.confirm.save_success.content")));
        }

        private void OnReturnMainMenuRequested()
        {
            UIComponent.Instance.OpenChild<ConfirmUI>(View, new ConfirmUIOpenData(
                LocalizationComponent.Instance.Get("ui.character.settings.return_main_menu"),
                LocalizationComponent.Instance.Get("ui.confirm.return_main_menu.content"),
                ConfirmReturnMainMenu));
        }

        private void ConfirmReturnMainMenu()
        {
            FlushSettings();
            GameFlowComponent.Instance.BeginTransition(new TransitionData
            {
                TargetSceneName = "MainMenu",
                TargetStateType = typeof(MainMenuState),
                TransitionUIName = "TransitionUI",
                KeepCurrentMainScene = true,
                ActiveSubSceneNames = System.Array.Empty<string>(),
            });
        }

        private void OnInventorySkillStoneDropped(CharacterInventoryDisplayData data, int insertIndex)
        {
            if (data == null || data.ItemType != ItemType.SkillStone)
                return;

            if (!PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;
            BackpackData backpackData = edit.Data.Backpack;
            SkillCData skillData = edit.Data.Skills;
            if (backpackData?.Items == null || skillData?.Chains == null)
                return;

            int skillChainIndex = UnityEngine.Mathf.Clamp(Model.SelectedChainIndex, 0, skillData.Chains.Length - 1);
            SkillChainData chain = skillData.Chains[skillChainIndex] ??= new SkillChainData { Index = skillChainIndex };
            chain.EnsureSlots();
            if (chain.IsFull)
            {
                UIComponent.Instance.Open<TipForm>(new TipFormOpenData
                {
                    Info = string.Format(LocalizationComponent.Resolve("ui.character.chain_full"), SkillChainData.MaxLength),
                });
                return;
            }
            if (!TryConsumeBackpackItem(backpackData, data.SlotIndex, data.ItemId, 1))
                return;

            int clampedInsertIndex = UnityEngine.Mathf.Clamp(insertIndex, 0, chain.Slots.Count);
            chain.Slots.Insert(clampedInsertIndex, new SkillChainSlotData
            {
                SkillStoneItemId = data.ItemId,
            });

            PlayerCharacterUtility.CommitEdit(edit);
        }

        private void OnInventoryEquipDropped(CharacterInventoryDisplayData data, int equipSlotIndex)
        {
            if (data == null || !IsEquippableItem(data.ItemType))
                return;

            if (!PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;
            BackpackData backpackData = edit.Data.Backpack;
            EquipmentData equipmentData = edit.Data.Equipment;
            if (backpackData?.Items == null || equipmentData == null)
                return;

            if (equipSlotIndex < 0 || equipSlotIndex >= 5)
                return;

            ItemData itemData = DataComponent.Instance.Get<ItemData>(data.ItemId);
            if (!CanEquipToSlot(itemData, equipSlotIndex))
                return;

            int oldItemId = EquipmentUtility.GetEquippedItemId(equipmentData, equipSlotIndex);
            if (!TryConsumeBackpackItem(backpackData, data.SlotIndex, data.ItemId, 1))
                return;

            if (oldItemId >= 0)
            {
                if (InventoryUtility.AddItemToBackpack(backpackData, oldItemId, 1) != 1)
                {
                    PublishBackpackFull();
                    return;
                }
            }

            EquipmentUtility.SetEquippedItemId(equipmentData, equipSlotIndex, data.ItemId);
            PlayerCharacterUtility.CommitEdit(edit);
        }

        private void OnInventoryItemMoved(CharacterInventoryDisplayData data, int targetSlotIndex)
        {
            if (data == null || !PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;

            if (InventoryUtility.TryMoveBackpackSlot(edit.Data.Backpack, data.SlotIndex, targetSlotIndex))
                PlayerCharacterUtility.CommitEdit(edit);
        }

        private void OnInventorySortRequested()
        {
            CloseEffectSelectUI();
            PlayerCharacterUtility.TrySortBackpack();
        }

        private void OnInventoryPropDropped(CharacterInventoryDisplayData data, int propSlotIndex)
        {
            if (data == null || data.ItemType != ItemType.Prop ||
                !PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;

            if (PropInventoryUtility.TryMoveBackpackToPropSlot(
                    edit.Data.Backpack,
                    edit.Data.Props,
                    data.SlotIndex,
                    propSlotIndex))
            {
                PlayerCharacterUtility.CommitEdit(edit);
            }
        }

        private void OnEquipReturnedToInventory(int equipSlotIndex, int inventorySlotIndex)
        {
            if (!PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;
            EquipmentData equipmentData = edit.Data.Equipment;
            BackpackData backpackData = edit.Data.Backpack;
            if (equipmentData == null || backpackData?.Items == null)
                return;

            int itemId = EquipmentUtility.GetEquippedItemId(equipmentData, equipSlotIndex);
            if (itemId < 0)
                return;

            ItemData itemData = DataComponent.Instance.Get<ItemData>(itemId);
            if (itemData == null || !IsEquippableItem(itemData.ItemType))
                return;

            if (!InventoryUtility.TryAddItemToBackpackSlot(
                    backpackData,
                    inventorySlotIndex,
                    itemId,
                    1,
                    itemData.ItemType))
            {
                PublishBackpackFull();
                return;
            }

            EquipmentUtility.SetEquippedItemId(equipmentData, equipSlotIndex, -1);
            PlayerCharacterUtility.CommitEdit(edit);
        }

        private void OnSpiritEquipSwapped(int sourceSlotIndex, int targetSlotIndex)
        {
            if (sourceSlotIndex < 1 || sourceSlotIndex > 4 || targetSlotIndex < 1 || targetSlotIndex > 4 || sourceSlotIndex == targetSlotIndex)
                return;

            if (!PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;
            EquipmentData equipmentData = edit.Data.Equipment;
            if (equipmentData == null)
                return;

            int sourceSpiritIndex = sourceSlotIndex - 1;
            int targetSpiritIndex = targetSlotIndex - 1;
            if (EquipmentUtility.SwapSpiritSlots(equipmentData, sourceSpiritIndex, targetSpiritIndex))
                PlayerCharacterUtility.CommitEdit(edit);
        }

        private void OnSkillReordered(CharacterSkillDisplayData data, int insertIndex)
        {
            if (data == null)
                return;

            if (!PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;
            SkillCData skillData = edit.Data.Skills;
            if (skillData?.Chains == null)
                return;

            int skillChainIndex = UnityEngine.Mathf.Clamp(Model.SelectedChainIndex, 0, skillData.Chains.Length - 1);
            SkillChainData chain = skillData.Chains[skillChainIndex];
            chain?.EnsureSlots();
            if (chain?.Slots == null || data.SkillIndex < 0 || data.SkillIndex >= chain.Slots.Count)
                return;

            int sourceIndex = data.SkillIndex;
            int targetIndex = UnityEngine.Mathf.Clamp(insertIndex, 0, chain.Slots.Count);
            if (sourceIndex < targetIndex)
                targetIndex--;

            if (targetIndex == sourceIndex)
                return;

            SkillChainSlotData slotData = chain.Slots[sourceIndex];
            chain.Slots.RemoveAt(sourceIndex);
            chain.Slots.Insert(targetIndex, slotData);
            PlayerCharacterUtility.CommitEdit(edit);
        }

        private void OnSkillReturnedToInventory(CharacterSkillDisplayData data, int inventorySlotIndex)
        {
            if (data == null)
                return;

            if (!PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;
            SkillCData skillData = edit.Data.Skills;
            BackpackData backpackData = edit.Data.Backpack;
            if (skillData?.Chains == null || backpackData?.Items == null)
                return;

            int skillChainIndex = UnityEngine.Mathf.Clamp(Model.SelectedChainIndex, 0, skillData.Chains.Length - 1);
            SkillChainData chain = skillData.Chains[skillChainIndex];
            chain?.EnsureSlots();
            if (chain?.Slots == null || data.SkillIndex < 0 || data.SkillIndex >= chain.Slots.Count)
                return;

            int skillId = chain.Slots[data.SkillIndex].SkillStoneItemId;
            if (!InventoryUtility.TryAddItemToBackpackSlot(
                    backpackData,
                    inventorySlotIndex,
                    skillId,
                    1,
                    ItemType.SkillStone))
            {
                PublishBackpackFull();
                return;
            }

            chain.Slots.RemoveAt(data.SkillIndex);
            PlayerCharacterUtility.CommitEdit(edit);
        }

        private void OnPropReturnedToInventory(int propSlotIndex, int inventorySlotIndex)
        {
            if (!PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;

            if (PropInventoryUtility.TryMovePropToBackpackSlot(
                    edit.Data.Backpack,
                    edit.Data.Props,
                    propSlotIndex,
                    inventorySlotIndex))
            {
                PlayerCharacterUtility.CommitEdit(edit);
                return;
            }

            PublishBackpackFull();
        }

        private void OnPropSlotMoved(int sourceSlotIndex, int targetSlotIndex)
        {
            if (!PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit) ||
                edit.Data.Props?.Slots == null ||
                sourceSlotIndex < 0 || sourceSlotIndex >= edit.Data.Props.Slots.Count ||
                targetSlotIndex < 0 || targetSlotIndex >= edit.Data.Props.Slots.Count ||
                sourceSlotIndex == targetSlotIndex)
            {
                return;
            }

            (edit.Data.Props.Slots[sourceSlotIndex], edit.Data.Props.Slots[targetSlotIndex]) =
                (edit.Data.Props.Slots[targetSlotIndex], edit.Data.Props.Slots[sourceSlotIndex]);
            PlayerCharacterUtility.CommitEdit(edit);
        }

        private void OnSkillAdditionRequested(CharacterSkillDisplayData data)
        {
            if (data == null || data.SkillIndex <= 0)
                return;

            if (!PlayerCharacterUtility.TryBeginEdit(out PlayerCharacterEdit edit))
                return;
            SkillCData skillData = edit.Data.Skills;
            if (skillData?.Chains == null)
                return;

            int skillChainIndex = UnityEngine.Mathf.Clamp(Model.SelectedChainIndex, 0, skillData.Chains.Length - 1);
            SkillChainData chain = skillData.Chains[skillChainIndex];
            chain?.EnsureSlots();
            if (chain?.Slots == null || data.SkillIndex < 0 || data.SkillIndex >= chain.Slots.Count)
                return;

            SkillChainSlotData slot = chain.Slots[data.SkillIndex];

            CloseEffectSelectUI();
            _effectSelectUI = UIComponent.Instance.OpenChild<EffectSelectUI>(View, new EffectSelectUIOpenData
            {
                Edit = edit,
                SkillChainIndex = skillChainIndex,
                SkillSlotIndex = data.SkillIndex,
                SelectedAdditionId = slot?.SkillAdditionId ?? -1,
            });
        }

        private void CloseEffectSelectUI()
        {
            if (_effectSelectUI == null)
                return;

            _effectSelectUI.Close();
            _effectSelectUI = null;
        }

        private bool IsEquippableItem(ItemType itemType)
        {
            return itemType == ItemType.MagicStone || itemType == ItemType.Spirit;
        }

        private bool CanEquipToSlot(ItemData itemData, int equipSlotIndex)
        {
            if (itemData == null || !IsEquippableItem(itemData.ItemType))
                return false;

            if (equipSlotIndex == 0)
                return itemData.ItemType == ItemType.MagicStone;

            if (equipSlotIndex >= 1 && equipSlotIndex <= 4)
                return itemData.ItemType == ItemType.Spirit;

            return false;
        }

        private bool TryConsumeBackpackItem(BackpackData backpackData, int slotIndex, int itemId, int count)
        {
            return InventoryUtility.TryConsumeBackpackItem(backpackData, slotIndex, itemId, count);
        }

        private static void PublishBackpackFull()
        {
            EventComponent.Instance.Publish(new PickupFeedbackEvent(
                PickupFeedbackType.BackpackFull,
                -1,
                0));
        }

    }
}
