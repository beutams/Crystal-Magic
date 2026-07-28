using CrystalMagic.Core;

namespace CrystalMagic.UI
{
    public sealed class SaveUIController : UIControllerBase<SaveUI, SaveUIModel>
    {
        public SaveUIController(SaveUI view, SaveUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            Bindings.Bind(() => View.BackClicked += OnBackClicked, () => View.BackClicked -= OnBackClicked);
            Bindings.Bind(() => View.SaveItemClicked += OnSaveItemClicked, () => View.SaveItemClicked -= OnSaveItemClicked);
            Bindings.Bind(() => View.SaveItemDeleteClicked += OnSaveItemDeleteClicked, () => View.SaveItemDeleteClicked -= OnSaveItemDeleteClicked);

            Model.SetSaveRecords(SaveDataComponent.Instance.GetAllSaveRecords());
        }

        private void OnBackClicked()
        {
            View.Close();
        }

        private void OnSaveItemClicked(int slotIndex)
        {
            CloseOpenedTip();

            SaveRecord record = Model.SaveRecords != null && slotIndex >= 0 && slotIndex < Model.SaveRecords.Length
                ? Model.SaveRecords[slotIndex]
                : null;

            string contentKey = record == null
                ? "ui.confirm.create_save.content"
                : "ui.confirm.overwrite_save.content";
            ConfirmUIOpenData openData = new(
                LocalizationComponent.Instance.Get("ui.confirm.save"),
                LocalizationComponent.Instance.Get(contentKey),
                () => EventComponent.Instance.Publish(new MainMenuStartRequestedEvent(slotIndex)),
                null);

            UIComponent.Instance.OpenChild<ConfirmUI>(View, openData);
        }

        private void OnSaveItemDeleteClicked(int slotIndex)
        {
            CloseOpenedTip();

            ConfirmUIOpenData openData = new(
                LocalizationComponent.Instance.Get("ui.confirm.delete_save"),
                LocalizationComponent.Instance.Get("ui.confirm.delete_save.content"),
                () => ConfirmDelete(slotIndex),
                null);

            UIComponent.Instance.OpenChild<ConfirmUI>(View, openData);
        }

        private void ConfirmDelete(int slotIndex)
        {
            SaveDataComponent.Instance.DeleteSlot(slotIndex);
            Model.SetSaveRecords(SaveDataComponent.Instance.GetAllSaveRecords());
        }

        private void CloseOpenedTip()
        {
            foreach (UIBase child in UIComponent.Instance.GetChildren(View))
            {
                if (child is ConfirmUI)
                {
                    UIComponent.Instance.ReleaseUI(child);
                }
            }
        }
    }
}
