using CrystalMagic.Core;

using CrystalMagic.UI;

public class GameMenuUI : UIBase<GameMenuUIData, GameMenuUIModel>
{
    public event System.Action SaveRequested;

    protected override void OnInit()
    {
        base.OnInit();
    }

    public override void OnOpen()
    {
        UI.Save.ButtonPlus.onClick.AddListener(OnSaveButton);
    }

    public override void OnClose()
    {
        UI.Save.ButtonPlus.onClick.RemoveListener(OnSaveButton);
    }

    private void OnSaveButton() => SaveRequested?.Invoke();
}
