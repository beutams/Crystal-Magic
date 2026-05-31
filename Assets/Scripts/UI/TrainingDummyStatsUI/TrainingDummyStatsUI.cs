using CrystalMagic.Core;
using CrystalMagic.UI;

public class TrainingDummyStatsUI : UIBase<TrainingDummyStatsUIData, TrainingDummyStatsUIModel>
{
    protected override void RefreshView()
    {
        if (Model == null || UI.Info.TextMeshProUGUI == null)
            return;

        UI.Info.TextMeshProUGUI.text = Model.DisplayText;
    }
}
