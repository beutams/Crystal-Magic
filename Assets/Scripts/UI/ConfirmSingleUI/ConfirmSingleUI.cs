using CrystalMagic.Core;

public class ConfirmSingleUI : UIBase<ConfirmSingleUIData, CrystalMagic.UI.ConfirmSingleUIModel>
{
    public event System.Action ConfirmClicked;

    public void SetTitle(string title)
    {
        UI.TitleBG_Title.TextMeshProUGUI.text = title ?? string.Empty;
    }

    public void SetContent(string content)
    {
        UI.Content.TextMeshProUGUI.text = content ?? string.Empty;
    }

    public void SetConfirmLabel(string label)
    {
        string value = label ?? string.Empty;
        UI.Confirm_Default_Text.TextMeshProUGUI.text = value;
        UI.Confirm_Click_Text.TextMeshProUGUI.text = value;
    }

    public override void OnOpen()
    {
        UI.Confirm.ButtonPlus.onClick.AddListener(OnConfirmButtonClicked);
        base.OnOpen();
    }

    public override void OnClose()
    {
        UI.Confirm.ButtonPlus.onClick.RemoveListener(OnConfirmButtonClicked);
        base.OnClose();
    }

    private void OnConfirmButtonClicked()
    {
        ConfirmClicked?.Invoke();
    }
}
