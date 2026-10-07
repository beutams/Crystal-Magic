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
            View.BackClicked += OnBackClicked;
            View.InventorySortRequested += OnInventorySortRequested;
            View.StashSortRequested += OnStashSortRequested;
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
            View.BackClicked -= OnBackClicked;
            View.InventorySortRequested -= OnInventorySortRequested;
            View.StashSortRequested -= OnStashSortRequested;
            CloseInteractUI();
        }

        private void OnAllCategoryRequested() => Model.SetCategory(StashCategory.All);
        private void OnSkillCategoryRequested() => Model.SetCategory(StashCategory.Skill);
        private void OnEquipCategoryRequested() => Model.SetCategory(StashCategory.Equip);
        private void OnPropsCategoryRequested() => Model.SetCategory(StashCategory.Props);
        private void OnBackClicked() => View.Close();

        private void OnInventorySortRequested()
        {
            CloseInteractUI();
            PlayerCharacterUtility.TrySortBackpack();
        }

        private void OnStashSortRequested()
        {
            CloseInteractUI();
            if (CrystalMagic.Core.InventoryUtility.SortStash(CrystalMagic.Core.SaveDataComponent.Instance.GetStashData()))
                CrystalMagic.Core.SaveDataComponent.Instance.NotifyStashDataChanged();
        }

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
