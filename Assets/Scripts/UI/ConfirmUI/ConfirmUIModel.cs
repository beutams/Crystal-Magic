namespace CrystalMagic.UI
{
    public readonly struct ConfirmUIOpenData
    {
        public ConfirmUIOpenData(
            string title,
            string content,
            System.Action confirmAction = null,
            System.Action cancelAction = null,
            string confirmLabel = "确定")
        {
            Title = title;
            Content = content;
            ConfirmAction = confirmAction;
            CancelAction = cancelAction;
            ConfirmLabel = confirmLabel;
        }

        public string Title { get; }
        public string Content { get; }
        public System.Action ConfirmAction { get; }
        public System.Action CancelAction { get; }
        public string ConfirmLabel { get; }
    }

    public sealed class ConfirmUIModel : UIModelBase, IUIOpenDataReceiver<ConfirmUIOpenData>
    {
        public string Title { get; private set; } = string.Empty;
        public string Content { get; private set; } = string.Empty;
        public System.Action ConfirmAction { get; private set; }
        public System.Action CancelAction { get; private set; }
        public string ConfirmLabel { get; private set; } = "确定";

        public void SetOpenData(ConfirmUIOpenData data)
        {
            Title = data.Title ?? string.Empty;
            Content = data.Content ?? string.Empty;
            ConfirmAction = data.ConfirmAction;
            CancelAction = data.CancelAction;
            ConfirmLabel = string.IsNullOrWhiteSpace(data.ConfirmLabel) ? "确定" : data.ConfirmLabel;
        }

        public override void Dispose()
        {
            ConfirmAction = null;
            CancelAction = null;
        }
    }
}
