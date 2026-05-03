namespace CrystalMagic.UI
{
    public sealed class StashUIController : UIControllerBase<StashUI, StashUIModel>
    {
        private StashInteractUI _interactUI;
        private readonly System.Action<CrystalMagic.Core.CommonGameEvent> _refreshHandler;

        public StashUIController(StashUI view, StashUIModel model)
            : base(view, model)
        {
            _refreshHandler = _ => Model.Refresh();
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            View.AllCategoryRequested += OnAllCategoryRequested;
            View.SkillCategoryRequested += OnSkillCategoryRequested;
            View.EquipCategoryRequested += OnEquipCategoryRequested;
            View.PropsCategoryRequested += OnPropsCategoryRequested;
            View.InventoryStoreRequested += OnInventoryStoreRequested;
            View.StashWithdrawRequested += OnStashWithdrawRequested;
            BindEvent(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.StashDataChangedEventName), _refreshHandler);
            BindEvent(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.BackpackDataChangedEventName), _refreshHandler);
            BindEvent(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.TownDataChangedEventName), _refreshHandler);
            Model.Refresh();
        }

        protected override void OnClose()
        {
            View.AllCategoryRequested -= OnAllCategoryRequested;
            View.SkillCategoryRequested -= OnSkillCategoryRequested;
            View.EquipCategoryRequested -= OnEquipCategoryRequested;
            View.PropsCategoryRequested -= OnPropsCategoryRequested;
            View.InventoryStoreRequested -= OnInventoryStoreRequested;
            View.StashWithdrawRequested -= OnStashWithdrawRequested;
            CloseInteractUI();
        }

        private void OnAllCategoryRequested() => Model.SetCategory(StashCategory.All);
        private void OnSkillCategoryRequested() => Model.SetCategory(StashCategory.Skill);
        private void OnEquipCategoryRequested() => Model.SetCategory(StashCategory.Equip);
        private void OnPropsCategoryRequested() => Model.SetCategory(StashCategory.Props);

        private void OnInventoryStoreRequested(StashInventoryDisplayData data)
        {
            if (data == null)
                return;

            CloseInteractUI();
            _interactUI = CrystalMagic.Core.UIComponent.Instance.OpenChild<StashInteractUI>(View, new StashInteractUIOpenData
            {
                Mode = StashInteractMode.Store,
                SourceSlotIndex = data.SlotIndex,
                ItemId = data.ItemId,
                Name = data.Name,
                HaveCount = data.Count,
                Description = data.Description,
                IconPath = data.IconPath,
            });
        }

        private void OnStashWithdrawRequested(StashItemDisplayData data)
        {
            if (data == null)
                return;

            CloseInteractUI();
            _interactUI = CrystalMagic.Core.UIComponent.Instance.OpenChild<StashInteractUI>(View, new StashInteractUIOpenData
            {
                Mode = StashInteractMode.Withdraw,
                SourceSlotIndex = data.SlotIndex,
                ItemId = data.ItemId,
                Name = data.Name,
                HaveCount = data.Count,
                Description = data.Description,
                IconPath = data.IconPath,
            });
        }

        private void CloseInteractUI()
        {
            if (_interactUI == null)
                return;

            _interactUI.Close();
            _interactUI = null;
        }
    }
}
