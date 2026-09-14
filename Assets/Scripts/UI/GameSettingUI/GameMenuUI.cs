using System;
using CrystalMagic.Core;
using CrystalMagic.UI;

public class GameMenuUI : UIBase<GameMenuUIData, GameMenuUIModel>
{
    public event Action ContinueRequested;
    public event Action SaveRequested;
    public event Action SettingsRequested;
    public event Action ReturnMainMenuRequested;

    public override void OnOpen()
    {
        UI.MenuBack_Continue.ButtonPlus.onClick.AddListener(OnContinueButton);
        UI.MenuBack_Load.ButtonPlus.onClick.AddListener(OnSaveButton);
        UI.MenuBack_Settings.ButtonPlus.onClick.AddListener(OnSettingsButton);
        UI.MenuBack_ReturnMainMenu.ButtonPlus.onClick.AddListener(OnReturnMainMenuButton);
        base.OnOpen();
    }

    public override void OnClose()
    {
        UI.MenuBack_Continue.ButtonPlus.onClick.RemoveListener(OnContinueButton);
        UI.MenuBack_Load.ButtonPlus.onClick.RemoveListener(OnSaveButton);
        UI.MenuBack_Settings.ButtonPlus.onClick.RemoveListener(OnSettingsButton);
        UI.MenuBack_ReturnMainMenu.ButtonPlus.onClick.RemoveListener(OnReturnMainMenuButton);
        base.OnClose();
    }

    private void OnContinueButton() => ContinueRequested?.Invoke();
    private void OnSaveButton() => SaveRequested?.Invoke();
    private void OnSettingsButton() => SettingsRequested?.Invoke();
    private void OnReturnMainMenuButton() => ReturnMainMenuRequested?.Invoke();
}
