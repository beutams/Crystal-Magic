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
        }

        private void OnSaveItemDeleteClicked(int slotIndex)
        {
            SaveDataComponent.Instance.DeleteSlot(slotIndex);
            Model.SetSaveRecords(SaveDataComponent.Instance.GetAllSaveRecords());
        }
    }
}
