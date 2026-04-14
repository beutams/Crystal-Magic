using System;
using CrystalMagic.Core;

/// <summary>
/// 主菜单
/// </summary>
public class MainMenuUI : UIBase<MainMenuUIData>
{
    public event Action StartRequested;
    public event Action LoadRequested;
    public event Action ConfigRequested;
    public event Action ExitRequested;

    protected override void OnInit()
    {
        base.OnInit();
    }

    public override void OnOpen()
    {
        UI.Start.ButtonPlus.onClick.AddListener(OnStartButton);
        UI.Load.ButtonPlus.onClick.AddListener(OnLoadButton);
        UI.Config.ButtonPlus.onClick.AddListener(OnConfigButton);
        UI.Exit.ButtonPlus.onClick.AddListener(OnExitButton);

    }

    public override void OnClose()
    {
        UI.Start.ButtonPlus.onClick.RemoveListener(OnStartButton);
        UI.Load.ButtonPlus.onClick.RemoveListener(OnLoadButton);
        UI.Config.ButtonPlus.onClick.RemoveListener(OnConfigButton);
        UI.Exit.ButtonPlus.onClick.RemoveListener(OnExitButton);

    }

    private void OnStartButton() => StartRequested?.Invoke();
    private void OnLoadButton() => LoadRequested?.Invoke();
    private void OnConfigButton() => ConfigRequested?.Invoke();
    private void OnExitButton() => ExitRequested?.Invoke();
}
