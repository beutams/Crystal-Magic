using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;

namespace CrystalMagic.UI
{
    public sealed class EffectSelectUIOpenData
    {
        public int SkillSlotIndex;
        public int SelectedAdditionId;
    }

    public sealed class EffectSelectAdditionDisplayData
    {
        public int AdditionId;
        public string Name;
        public string Description;
        public string IconPath;
        public bool IsSelected;
    }

    public sealed class EffectSelectUIModel : UIModelBase, IUIOpenDataReceiver<EffectSelectUIOpenData>
    {
        public const string DataChangedEventName = "EffectSelectUIModel.DataChanged";
        public override string ChangedEventName => DataChangedEventName;

        private readonly List<EffectSelectAdditionDisplayData> _items = new();

        public IReadOnlyList<EffectSelectAdditionDisplayData> Items => _items;
        public int SkillSlotIndex { get; private set; }
        public int SelectedAdditionId { get; private set; }

        public void SetOpenData(EffectSelectUIOpenData data)
        {
            SkillSlotIndex = data != null ? data.SkillSlotIndex : 0;
            SelectedAdditionId = data != null ? data.SelectedAdditionId : -1;
            Refresh();
        }

        public void Refresh()
        {
            _items.Clear();

            DataTable<SkillAdditionData> table = DataComponent.Instance.GetTable<SkillAdditionData>();
            IEnumerable<SkillAdditionData> allEffects = table?.GetAll();
            if (allEffects != null)
            {
                List<SkillAdditionData> sortedEffects = new(allEffects);
                sortedEffects.Sort((left, right) => left.Id.CompareTo(right.Id));
                for (int i = 0; i < sortedEffects.Count; i++)
                {
                    SkillAdditionData effectData = sortedEffects[i];
                    if (effectData == null)
                        continue;

                    _items.Add(new EffectSelectAdditionDisplayData
                    {
                        AdditionId = effectData.Id,
                        Name = effectData.Name,
                        Description = effectData.Description,
                        IconPath = effectData.IconPath,
                        IsSelected = effectData.Id == SelectedAdditionId,
                    });
                }
            }

            EventComponent.Instance.Publish(new CommonGameEvent(DataChangedEventName, this));
        }
    }
}
