namespace CrystalMagic.UI
{
    public sealed class CharacterUIController : UIControllerBase<CharacterUI, CharacterUIModel>
    {
        private readonly System.Action<CrystalMagic.Core.CommonGameEvent> _refreshHandler;

        public CharacterUIController(CharacterUI view, CharacterUIModel model)
            : base(view, model)
        {
            _refreshHandler = _ => Model.Refresh();
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            View.ChangeSkillRequested += OnChangeSkillRequested;
            CrystalMagic.Core.EventComponent.Instance.Subscribe(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.RuntimeDataComponent.SkillRuntimeDataChangedEventName), _refreshHandler);
            CrystalMagic.Core.EventComponent.Instance.Subscribe(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.SkillDataChangedEventName), _refreshHandler);
            CrystalMagic.Core.EventComponent.Instance.Subscribe(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.BackpackDataChangedEventName), _refreshHandler);
            CrystalMagic.Core.EventComponent.Instance.Subscribe(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.EquipmentDataChangedEventName), _refreshHandler);
            Model.Refresh();
        }

        protected override void OnClose()
        {
            View.ChangeSkillRequested -= OnChangeSkillRequested;
            CrystalMagic.Core.EventComponent.Instance.Unsubscribe(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.RuntimeDataComponent.SkillRuntimeDataChangedEventName), _refreshHandler);
            CrystalMagic.Core.EventComponent.Instance.Unsubscribe(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.SkillDataChangedEventName), _refreshHandler);
            CrystalMagic.Core.EventComponent.Instance.Unsubscribe(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.BackpackDataChangedEventName), _refreshHandler);
            CrystalMagic.Core.EventComponent.Instance.Unsubscribe(new CrystalMagic.Core.CommonGameEvent(CrystalMagic.Core.SaveDataComponent.EquipmentDataChangedEventName), _refreshHandler);
        }

        private void OnChangeSkillRequested()
        {
            CrystalMagic.Core.RuntimeDataComponent.Instance.SelectNextSkillChain(CrystalMagic.Core.SaveDataComponent.Instance.GetSkillData());
        }
    }
}
