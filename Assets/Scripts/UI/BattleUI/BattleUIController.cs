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
            View.PropShortcutUseRequested += OnPropShortcutUseRequested;
            View.PropShortcutBindRequested += OnPropShortcutBindRequested;
            BindEvent(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.SkillDataChangedEventName), _refreshHandler);
            BindEvent(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.CharacterPropDataChangedEventName), _refreshHandler);
            BindEvent(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.RuntimeDataComponent.SkillRuntimeDataChangedEventName), _refreshHandler);
            BindEvent(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.RuntimeDataComponent.PropRuntimeDataChangedEventName), _refreshHandler);
            Model.Refresh();
        }

        protected override void OnClose()
        {
            View.PropShortcutUseRequested -= OnPropShortcutUseRequested;
            View.PropShortcutBindRequested -= OnPropShortcutBindRequested;
        }

        private void OnPropShortcutUseRequested(int shortcutIndex)
        {
            CrystalMagic.Game.PropUseUtility.TryUseShortcutSlot(shortcutIndex, out _);
        }

        private void OnPropShortcutBindRequested(int propSlotIndex, int shortcutIndex)
        {
            CrystalMagic.Game.PropUseUtility.TryBindShortcutSlot(shortcutIndex, propSlotIndex);
        }
    }
}
