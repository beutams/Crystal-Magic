using CrystalMagic.Core;

namespace CrystalMagic.UI
{
    public sealed class SaveUIModel : UIModelBase
    {
        public const string SaveRecordsChangedEventName = "SaveUIModel.SaveRecordsChanged";

        private const int SlotCount = 20;
        private readonly SaveRecord[] _saveRecords = new SaveRecord[SlotCount];

        public int SlotCountValue => SlotCount;
        public SaveRecord[] SaveRecords => _saveRecords;

        public void SetSaveRecords(System.Collections.Generic.IEnumerable<SaveRecord> records)
        {
            System.Array.Clear(_saveRecords, 0, _saveRecords.Length);

            if (records != null)
            {
                foreach (SaveRecord record in records)
                {
                    if (record == null)
                        continue;

                    if (record.SaveIndex < 0 || record.SaveIndex >= SlotCount)
                        continue;

                    _saveRecords[record.SaveIndex] = record;
                }
            }

            EventComponent.Instance.Publish(new CommonGameEvent(SaveRecordsChangedEventName, this));
        }
    }
}
