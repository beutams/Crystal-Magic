namespace CrystalMagic.UI
{
    public sealed class LoadUIController : UIControllerBase<LoadUI, LoadUIModel>
    {
        public LoadUIController(LoadUI view, LoadUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            Bindings.Bind(() => View.BackClicked += OnBackClicked, () => View.BackClicked -= OnBackClicked);
            Bindings.Bind(() => View.SaveItemClicked += OnSaveItemClicked, () => View.SaveItemClicked -= OnSaveItemClicked);
            Bindings.Bind(() => View.SaveItemDeleteClicked += OnSaveItemDeleteClicked, () => View.SaveItemDeleteClicked -= OnSaveItemDeleteClicked);

            Model.SetSaveRecords(CrystalMagic.Core.SaveDataComponent.Instance.GetAllSaveRecords());
        }

        private void OnBackClicked()
        {
            View.Close();
        }

        private void OnSaveItemClicked(int slotIndex)
        {
            CrystalMagic.Core.SaveRecord record = Model.SaveRecords != null && slotIndex >= 0 && slotIndex < Model.SaveRecords.Length
                ? Model.SaveRecords[slotIndex]
                : null;

            if (record == null)
                return;

            CloseOpenedTip();

            ConfirmUIOpenData openData = new(
                CrystalMagic.Core.LocalizationComponent.Instance.Get("ui.confirm.load"),
                CrystalMagic.Core.LocalizationComponent.Instance.Get("ui.confirm.load_save.content"),
                () => CrystalMagic.Core.EventComponent.Instance.Publish(new CrystalMagic.Core.MainMenuLoadRequestedEvent(slotIndex)),
                null);

            CrystalMagic.Core.UIComponent.Instance.OpenChild<ConfirmUI>(View, openData);
        }

        private void OnSaveItemDeleteClicked(int slotIndex)
        {
            CloseOpenedTip();

            ConfirmUIOpenData openData = new(
                CrystalMagic.Core.LocalizationComponent.Instance.Get("ui.confirm.delete_save"),
                CrystalMagic.Core.LocalizationComponent.Instance.Get("ui.confirm.delete_save.content"),
                () => ConfirmDelete(slotIndex),
                null);

            CrystalMagic.Core.UIComponent.Instance.OpenChild<ConfirmUI>(View, openData);
        }

        private void ConfirmDelete(int slotIndex)
        {
            CrystalMagic.Core.SaveDataComponent.Instance.DeleteSlot(slotIndex);
            Model.SetSaveRecords(CrystalMagic.Core.SaveDataComponent.Instance.GetAllSaveRecords());
        }

        private void CloseOpenedTip()
        {
            foreach (CrystalMagic.Core.UIBase child in CrystalMagic.Core.UIComponent.Instance.GetChildren(View))
            {
                if (child is ConfirmUI)
                {
                    CrystalMagic.Core.UIComponent.Instance.ReleaseUI(child);
                }
            }
        }
    }
}
