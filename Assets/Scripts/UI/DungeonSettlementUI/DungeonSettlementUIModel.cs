using CrystalMagic.Core;

namespace CrystalMagic.UI
{
    public sealed class DungeonSettlementUIOpenData
    {
        public string Title;
        public string Summary;
        public string ConfirmLabel;
        public System.Action ConfirmAction;
    }

    public sealed class DungeonSettlementUIModel : UIModelBase, IUIOpenDataReceiver<DungeonSettlementUIOpenData>
    {
        public const string DataChangedEventName = "DungeonSettlementUIModel.DataChanged";

        public override string ChangedEventName => DataChangedEventName;

        public string Title { get; private set; } = string.Empty;
        public string Summary { get; private set; } = string.Empty;
        public string ConfirmLabel { get; private set; } = "返回城镇";
        public System.Action ConfirmAction { get; private set; }

        public void SetOpenData(DungeonSettlementUIOpenData data)
        {
            Title = data?.Title ?? string.Empty;
            Summary = data?.Summary ?? string.Empty;
            ConfirmLabel = string.IsNullOrWhiteSpace(data?.ConfirmLabel) ? "返回城镇" : data.ConfirmLabel;
            ConfirmAction = data?.ConfirmAction;
            EventComponent.Instance.Publish(new CommonGameEvent(DataChangedEventName, this));
        }

        public override void Dispose()
        {
            ConfirmAction = null;
        }
    }
}
