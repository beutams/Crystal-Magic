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
        UI.Start.Button.onClick.AddListener(OnStartButton);
        UI.Load.Button.onClick.AddListener(OnLoadButton);
        UI.Config.Button.onClick.AddListener(OnConfigButton);
        UI.Exit.Button.onClick.AddListener(OnExitButton);

    }

    public override void OnClose()
    {
        UI.Start.Button.onClick.RemoveListener(OnStartButton);
        UI.Load.Button.onClick.RemoveListener(OnLoadButton);
        UI.Config.Button.onClick.RemoveListener(OnConfigButton);
        UI.Exit.Button.onClick.RemoveListener(OnExitButton);

    }

    private void OnStartButton() => StartRequested?.Invoke();
    private void OnLoadButton() => LoadRequested?.Invoke();
    private void OnConfigButton() => ConfigRequested?.Invoke();
    private void OnExitButton() => ExitRequested?.Invoke();
}
