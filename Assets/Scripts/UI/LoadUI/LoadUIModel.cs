namespace CrystalMagic.UI
{
    public sealed class LoadUIModel : UIModelBase
    {
        public const string SaveRecordsChangedEventName = "LoadUIModel.SaveRecordsChanged";
        public override string ChangedEventName => SaveRecordsChangedEventName;

        private const int SlotCount = 20;
        private readonly CrystalMagic.Core.SaveRecord[] _saveRecords = new CrystalMagic.Core.SaveRecord[SlotCount];

        public int SlotCountValue => SlotCount;
        public CrystalMagic.Core.SaveRecord[] SaveRecords => _saveRecords;

        public void SetSaveRecords(System.Collections.Generic.IEnumerable<CrystalMagic.Core.SaveRecord> records)
        {
            System.Array.Clear(_saveRecords, 0, _saveRecords.Length);

            if (records != null)
            {
                foreach (CrystalMagic.Core.SaveRecord record in records)
                {
                    if (record == null)
                        continue;

                    if (record.SaveIndex < 0 || record.SaveIndex >= SlotCount)
                        continue;

                    _saveRecords[record.SaveIndex] = record;
                }
            }

            CrystalMagic.Core.EventComponent.Instance.Publish(new CrystalMagic.Core.CommonGameEvent(SaveRecordsChangedEventName, this));
        }
    }
}
