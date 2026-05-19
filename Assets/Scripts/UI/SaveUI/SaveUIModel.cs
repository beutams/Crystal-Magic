using CrystalMagic.Core;
using CrystalMagic.Game.Config;

namespace CrystalMagic.UI
{
    public sealed class SaveUIModel : UIModelBase
    {
        public const string SaveRecordsChangedEventName = "SaveUIModel.SaveRecordsChanged";
        public override string ChangedEventName => SaveRecordsChangedEventName;

        private SaveRecord[] _saveRecords = System.Array.Empty<SaveRecord>();

        public int SlotCountValue => _saveRecords.Length;
        public SaveRecord[] SaveRecords => _saveRecords;

        public void SetSaveRecords(System.Collections.Generic.IEnumerable<SaveRecord> records)
        {
            EnsureSlotArray();
            System.Array.Clear(_saveRecords, 0, _saveRecords.Length);

            if (records != null)
            {
                foreach (SaveRecord record in records)
                {
                    if (record == null)
                        continue;

                    if (record.SaveIndex < 0 || record.SaveIndex >= _saveRecords.Length)
                        continue;

                    _saveRecords[record.SaveIndex] = record;
                }
            }

            EventComponent.Instance.Publish(new CommonGameEvent(SaveRecordsChangedEventName, this));
        }

        private void EnsureSlotArray()
        {
            int slotCount = UnityEngine.Mathf.Max(1, ConfigComponent.Instance.Get<GameConfig>().MaxSaveSlots);

            if (_saveRecords.Length != slotCount)
                _saveRecords = new SaveRecord[slotCount];
        }
    }
}
