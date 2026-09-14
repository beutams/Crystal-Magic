using CrystalMagic.Core;

using System;

public class ConfirmUI : UIBase<ConfirmUIData>
{
    public event Action ConfirmClicked;
    public event Action CancelClicked;

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

    public void SetCancelVisible(bool isVisible)
    {
        UI.Cancel.GameObject.SetActive(isVisible);
    }

    public override void OnOpen()
    {
        UI.Confirm.ButtonPlus.onClick.AddListener(OnConfirmButtonClicked);
        UI.Cancel.ButtonPlus.onClick.AddListener(OnCancelButtonClicked);
    }

    public override void OnClose()
    {
        UI.Confirm.ButtonPlus.onClick.RemoveListener(OnConfirmButtonClicked);
        UI.Cancel.ButtonPlus.onClick.RemoveListener(OnCancelButtonClicked);
    }

    private void OnConfirmButtonClicked()
    {
        ConfirmClicked?.Invoke();
    }

    private void OnCancelButtonClicked()
    {
        CancelClicked?.Invoke();
    }
}
