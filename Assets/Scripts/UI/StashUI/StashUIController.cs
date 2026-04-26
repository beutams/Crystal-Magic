namespace CrystalMagic.UI
{
    public sealed class StashUIController : UIControllerBase<StashUI, StashUIModel>
    {
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
            CrystalMagic.Core.EventComponent.Instance.Subscribe(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.StashDataChangedEventName), _refreshHandler);
            CrystalMagic.Core.EventComponent.Instance.Subscribe(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.BackpackDataChangedEventName), _refreshHandler);
            CrystalMagic.Core.EventComponent.Instance.Subscribe(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.TownDataChangedEventName), _refreshHandler);
            Model.Refresh();
        }

        protected override void OnClose()
        {
            View.AllCategoryRequested -= OnAllCategoryRequested;
            View.SkillCategoryRequested -= OnSkillCategoryRequested;
            View.EquipCategoryRequested -= OnEquipCategoryRequested;
            View.PropsCategoryRequested -= OnPropsCategoryRequested;
            CrystalMagic.Core.EventComponent.Instance.Unsubscribe(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.StashDataChangedEventName), _refreshHandler);
            CrystalMagic.Core.EventComponent.Instance.Unsubscribe(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.BackpackDataChangedEventName), _refreshHandler);
            CrystalMagic.Core.EventComponent.Instance.Unsubscribe(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.TownDataChangedEventName), _refreshHandler);
        }

        private void OnAllCategoryRequested() => Model.SetCategory(StashCategory.All);
        private void OnSkillCategoryRequested() => Model.SetCategory(StashCategory.Skill);
        private void OnEquipCategoryRequested() => Model.SetCategory(StashCategory.Equip);
        private void OnPropsCategoryRequested() => Model.SetCategory(StashCategory.Props);
    }
}
