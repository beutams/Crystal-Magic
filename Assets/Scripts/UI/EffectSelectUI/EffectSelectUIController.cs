namespace CrystalMagic.UI
{
    public sealed class EffectSelectUIController : UIControllerBase<EffectSelectUI, EffectSelectUIModel>
    {
        private EffectItemInfoUI _itemInfoUI;

        public EffectSelectUIController(EffectSelectUI view, EffectSelectUIModel model)
            : base(view, model)
        {
        }

        protected override void OnOpen()
        {
            View.BindModel(Model);
            View.ItemHoverReady += OnItemHovered;
            View.ItemHoverExited += OnItemHoverExited;
            View.ItemSelected += OnItemSelected;
            Model.Refresh();
        }

        protected override void OnClose()
        {
            View.ItemHoverReady -= OnItemHovered;
            View.ItemHoverExited -= OnItemHoverExited;
            View.ItemSelected -= OnItemSelected;
            CloseItemInfoUI();
        }

        private void OnItemHovered(EffectSelectAdditionDisplayData data)
        {
            if (data == null)
                return;

            CloseItemInfoUI();
            _itemInfoUI = CrystalMagic.Core.UIComponent.Instance.OpenChild<EffectItemInfoUI>(View, new EffectItemInfoUIOpenData
            {
                Name = data.Name,
                Description = data.Description,
                IconPath = data.IconPath,
            });
        }

        private void OnItemHoverExited()
        {
            CloseItemInfoUI();
        }

        private void OnItemSelected(EffectSelectAdditionDisplayData data)
        {
            if (data == null)
                return;

            CrystalMagic.Core.SkillCData skillData = Model.Edit?.Data.Skills;
            if (skillData?.Chains == null)
                return;

            int skillChainIndex = Model.SkillChainIndex;
            CrystalMagic.Core.SkillChainData chain = skillData.Chains[skillChainIndex];
            chain?.EnsureSlots();
            if (chain?.Slots == null || Model.SkillSlotIndex < 0 || Model.SkillSlotIndex >= chain.Slots.Count)
                return;

            CrystalMagic.Core.SkillChainSlotData slot = chain.Slots[Model.SkillSlotIndex];
            if (slot == null)
                return;

            slot.SkillAdditionId = data.AdditionId;
            PlayerCharacterUtility.CommitEdit(Model.Edit);
            View.Close();
        }

        private void CloseItemInfoUI()
        {
            if (_itemInfoUI == null)
                return;

            _itemInfoUI.Close();
            _itemInfoUI = null;
        }

    }
}
