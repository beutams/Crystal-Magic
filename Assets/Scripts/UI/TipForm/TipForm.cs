using CrystalMagic.Core;

public class TipForm : UIBase<TipFormData, CrystalMagic.UI.TipFormModel>
{
    protected override void RefreshView()
    {
        if (Model == null || UI.Info.TextMeshProUGUI == null)
            return;

        UI.Info.TextMeshProUGUI.text = Model.Info;
    }
}
