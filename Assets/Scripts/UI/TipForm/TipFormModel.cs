namespace CrystalMagic.UI
{
    public sealed class TipFormOpenData
    {
        public string Info;
    }

    public sealed class TipFormModel : UIModelBase, IUIOpenDataReceiver<TipFormOpenData>
    {
        public const string DataChangedEventName = "TipFormModel.DataChanged";
        public override string ChangedEventName => DataChangedEventName;

        public string Info { get; private set; }

        public void SetOpenData(TipFormOpenData data)
        {
            Info = data != null ? data.Info : string.Empty;
            CrystalMagic.Core.EventComponent.Instance.Publish(new CrystalMagic.Core.CommonGameEvent(DataChangedEventName, this));
        }
    }
}
