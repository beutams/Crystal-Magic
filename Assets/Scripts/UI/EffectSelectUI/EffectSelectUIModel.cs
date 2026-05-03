using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;

namespace CrystalMagic.UI
{
    public sealed class EffectSelectUIOpenData
    {
        public int SkillSlotIndex;
        public int SelectedEffectId;
    }

    public sealed class EffectSelectEffectDisplayData
    {
        public int EffectId;
        public string Name;
        public string Description;
        public string IconPath;
        public bool IsSelected;
    }

    public sealed class EffectSelectUIModel : UIModelBase, IUIOpenDataReceiver<EffectSelectUIOpenData>
    {
        public const string DataChangedEventName = "EffectSelectUIModel.DataChanged";
        public override string ChangedEventName => DataChangedEventName;

        private readonly List<EffectSelectEffectDisplayData> _items = new();

        public IReadOnlyList<EffectSelectEffectDisplayData> Items => _items;
        public int SkillSlotIndex { get; private set; }
        public int SelectedEffectId { get; private set; }

        public void SetOpenData(EffectSelectUIOpenData data)
        {
            SkillSlotIndex = data != null ? data.SkillSlotIndex : 0;
            SelectedEffectId = data != null ? data.SelectedEffectId : 0;
            Refresh();
        }

        public void Refresh()
        {
            _items.Clear();

            DataTable<SkillEffectData> table = DataComponent.Instance.GetTable<SkillEffectData>();
            IEnumerable<SkillEffectData> allEffects = table?.GetAll();
            if (allEffects != null)
            {
                List<SkillEffectData> sortedEffects = new(allEffects);
                sortedEffects.Sort((left, right) => left.Id.CompareTo(right.Id));
                for (int i = 0; i < sortedEffects.Count; i++)
                {
                    SkillEffectData effectData = sortedEffects[i];
                    if (effectData == null)
                        continue;

                    _items.Add(new EffectSelectEffectDisplayData
                    {
                        EffectId = effectData.Id,
                        Name = effectData.Name,
                        Description = effectData.Description,
                        IconPath = effectData.IconPath,
                        IsSelected = effectData.Id == SelectedEffectId,
                    });
                }
            }

            EventComponent.Instance.Publish(new CommonGameEvent(DataChangedEventName, this));
        }
    }
}
