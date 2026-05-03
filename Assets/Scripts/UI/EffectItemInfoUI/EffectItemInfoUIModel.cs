namespace CrystalMagic.UI
{
    public sealed class EffectItemInfoUIOpenData
    {
        public string Name;
        public string Description;
        public string IconPath;
    }

    public sealed class EffectItemInfoUIModel : UIModelBase, IUIOpenDataReceiver<EffectItemInfoUIOpenData>
    {
        public const string DataChangedEventName = "EffectItemInfoUIModel.DataChanged";
        public override string ChangedEventName => DataChangedEventName;

        public string Name { get; private set; }
        public string Description { get; private set; }
        public string IconPath { get; private set; }

        public void SetOpenData(EffectItemInfoUIOpenData data)
        {
            Name = data != null ? data.Name : string.Empty;
            Description = data != null ? data.Description : string.Empty;
            IconPath = data != null ? data.IconPath : string.Empty;
            CrystalMagic.Core.EventComponent.Instance.Publish(new CrystalMagic.Core.CommonGameEvent(DataChangedEventName, this));
        }
    }
}
