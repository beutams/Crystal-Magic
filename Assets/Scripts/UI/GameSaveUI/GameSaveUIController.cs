namespace CrystalMagic.UI
{
    using CrystalMagic.Core;

    public sealed class GameSaveUIController : UIControllerBase<GameSaveUI, GameSaveUIModel>
    {
        public GameSaveUIController(GameSaveUI view, GameSaveUIModel model)
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

            string content = record == null ? "是否创建新存档？" : "是否覆盖该存档？";
            ConfirmUIOpenData openData = new(
                "保存",
                content,
                () => ConfirmSave(slotIndex),
                null);

            UIComponent.Instance.OpenChild<ConfirmUI>(View, openData);
        }

        private void ConfirmSave(int slotIndex)
        {
            if (SaveDataComponent.Instance.SaveToSlot(slotIndex))
                Model.SetSaveRecords(SaveDataComponent.Instance.GetAllSaveRecords());
        }

        private void OnSaveItemDeleteClicked(int slotIndex)
        {
            SaveDataComponent.Instance.DeleteSlot(slotIndex);
            Model.SetSaveRecords(SaveDataComponent.Instance.GetAllSaveRecords());
        }

        private void CloseOpenedTip()
        {
            foreach (UIBase child in UIComponent.Instance.GetChildren(View))
            {
                if (child is ConfirmUI)
                    UIComponent.Instance.ReleaseUI(child);
            }
        }
    }
}
