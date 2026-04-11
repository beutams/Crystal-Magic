using CrystalMagic.Core;
using UnityEngine;

public class MainMenuUI : UIBase<MainMenuUIData>
{
    protected override void OnInit()
    {
        base.OnInit();
    }

    public override void OnOpen()
    {
        UI.Start.Button.onClick.AddListener(OnStartClicked);
        UI.Load.Button.onClick.AddListener(OnLoadClicked);
        UI.Config.Button.onClick.AddListener(OnConfigClicked);
        UI.Exit.Button.onClick.AddListener(OnExitClicked);
    }

    public override void OnClose()
    {
        UI.Start.Button.onClick.RemoveListener(OnStartClicked);
        UI.Load.Button.onClick.RemoveListener(OnLoadClicked);
        UI.Config.Button.onClick.RemoveListener(OnConfigClicked);
        UI.Exit.Button.onClick.RemoveListener(OnExitClicked);
    }

    // ─────────────────────────────────────────
    //  按钮回调
    // ─────────────────────────────────────────

    private void OnStartClicked()
    {
        // 创建新存档并保存，然后进入城镇流程
        SaveDataComponent.Instance.Save();
        GetMainMenuState()?.GoToTown();
    }

    private void OnLoadClicked()
    {
        GetMainMenuState()?.StartLoadGame();
    }

    private void OnConfigClicked()
    {
        // TODO: 打开设置 UI
        Debug.Log("[MainMenuUI] Config clicked");
    }

    private void OnExitClicked()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // ─────────────────────────────────────────
    private static MainMenuState GetMainMenuState()
        => GameFlowComponent.Instance.GetCurrentState() as MainMenuState;
}
