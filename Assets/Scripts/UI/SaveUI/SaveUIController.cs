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
            BindModelChanged(Model, RefreshView);
            Bindings.Bind(() => View.BackClicked += OnBackClicked, () => View.BackClicked -= OnBackClicked);
            Bindings.Bind(() => View.SaveItemClicked += OnSaveItemClicked, () => View.SaveItemClicked -= OnSaveItemClicked);

            Model.SetSaveRecords(SaveDataComponent.Instance.GetAllSaveRecords());
        }

        private void RefreshView()
        {
            View.RenderSlots(Model.SaveRecords, Model.SlotCountValue);
        }

        private void OnBackClicked()
        {
            View.Close();
        }

        private void OnSaveItemClicked(int slotIndex)
        {
        }
    }
}
