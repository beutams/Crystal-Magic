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
            CrystalMagic.Core.InputComponent.Instance.OnUseProp += OnPropShortcutUseRequested;
            BindEvent(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.SkillDataChangedEventName), _refreshHandler);
            BindEvent(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.CharacterPropDataChangedEventName), _refreshHandler);
            BindEvent(new CrystalMagic.Core.CommonGameEvent(PlayerInputComponent.SkillChainChangedEventName), _refreshHandler);
            Model.Refresh();
        }

        protected override void OnClose()
        {
            View.PropShortcutUseRequested -= OnPropShortcutUseRequested;
            CrystalMagic.Core.InputComponent.Instance.OnUseProp -= OnPropShortcutUseRequested;
        }

        private void OnPropShortcutUseRequested(int shortcutIndex)
        {
            CrystalMagic.Game.PropUseUtility.TryUseShortcutSlot(shortcutIndex, out _);
        }

    }
}
