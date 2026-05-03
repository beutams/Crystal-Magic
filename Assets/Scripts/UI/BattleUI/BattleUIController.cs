namespace CrystalMagic.UI
{
    public sealed class BattleUIController : UIControllerBase<BattleUI, BattleUIModel>
    {
        private readonly System.Action<CrystalMagic.Core.CommonGameEvent> _refreshHandler;

        public BattleUIController(BattleUI view, BattleUIModel model)
            : base(view, model)
        {
            _refreshHandler = _ => Model.Refresh();
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            BindEvent(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.SkillDataChangedEventName), _refreshHandler);
            BindEvent(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.RuntimeDataComponent.SkillRuntimeDataChangedEventName), _refreshHandler);
            Model.Refresh();
        }
    }
}
