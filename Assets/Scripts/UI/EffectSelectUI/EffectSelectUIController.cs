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

            CrystalMagic.Core.SkillCData skillData = CrystalMagic.Core.SaveDataComponent.Instance.GetSkillData();
            CrystalMagic.Core.RuntimeSkillData runtimeSkillData = CrystalMagic.Core.RuntimeDataComponent.Instance.GetSkillData();
            if (skillData?.Chains == null || runtimeSkillData == null)
                return;

            int skillChainIndex = UnityEngine.Mathf.Clamp(runtimeSkillData.CurrentSkillChainIndex, 0, skillData.Chains.Length - 1);
            CrystalMagic.Core.SkillChainData chain = skillData.Chains[skillChainIndex];
            chain?.EnsureSlots();
            if (chain?.Slots == null || Model.SkillSlotIndex < 0 || Model.SkillSlotIndex >= chain.Slots.Count)
                return;

            chain.Slots[Model.SkillSlotIndex].SkillAdditionId = data.AdditionId;
            CrystalMagic.Core.SaveDataComponent.Instance.NotifySkillDataChanged();
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
