using System;
using CrystalMagic.Core;

public sealed class DungeonSettlementUI : UIBase<DungeonSettlementUIData, CrystalMagic.UI.DungeonSettlementUIModel>
{
    public event Action ConfirmClicked;

    public override void OnOpen()
    {
        base.OnOpen();
        UI.Confirm.ButtonPlus.onClick.AddListener(OnConfirmButtonClicked);
    }

    public override void OnClose()
    {
        UI.Confirm.ButtonPlus.onClick.RemoveListener(OnConfirmButtonClicked);
        base.OnClose();
    }

    protected override void RefreshView()
    {
        UI.Title.TextMeshProUGUI.text = Model?.Title ?? string.Empty;
        UI.Summary.TextMeshProUGUI.text = Model?.Summary ?? string.Empty;
        UI.Confirm_Label.TextMeshProUGUI.text = Model?.ConfirmLabel ?? string.Empty;
    }

    private void OnConfirmButtonClicked()
    {
        ConfirmClicked?.Invoke();
    }
}
