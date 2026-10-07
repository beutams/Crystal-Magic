using CrystalMagic.Core;

namespace CrystalMagic.UI
{
    public sealed class SaveUIModel : UIModelBase
    {
        public const string SaveRecordsChangedEventName = "SaveUIModel.SaveRecordsChanged";
        public override string ChangedEventName => SaveRecordsChangedEventName;

        public const int SlotCount = 3;
        private readonly SaveRecord[] _saveRecords = new SaveRecord[SlotCount];

        public int SlotCountValue => _saveRecords.Length;
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

                    if (record.SaveIndex < 0 || record.SaveIndex >= _saveRecords.Length)
                        continue;

                    _saveRecords[record.SaveIndex] = record;
                }
            }

            EventComponent.Instance.Publish(new CommonGameEvent(SaveRecordsChangedEventName, this));
        }

    }
}
