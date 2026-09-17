namespace CrystalMagic.UI
{
    public sealed class ConfirmSingleUIModel : UIModelBase, IUIOpenDataReceiver<ConfirmUIOpenData>
    {
        public string Title { get; private set; } = string.Empty;
        public string Content { get; private set; } = string.Empty;
        public System.Action ConfirmAction { get; private set; }
        public string ConfirmLabel { get; private set; } = "OK";

        public void SetOpenData(ConfirmUIOpenData data)
        {
            Title = data.Title ?? string.Empty;
            Content = data.Content ?? string.Empty;
            ConfirmAction = data.ConfirmAction;
            ConfirmLabel = string.IsNullOrWhiteSpace(data.ConfirmLabel) ? "OK" : data.ConfirmLabel;
        }

        public override void Dispose()
        {
            ConfirmAction = null;
        }
    }
}
