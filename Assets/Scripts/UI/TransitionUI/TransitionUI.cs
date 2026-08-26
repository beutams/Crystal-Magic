using System.Collections;
using CrystalMagic.Core;
using CrystalMagic.Game.Config;
using UnityEngine;

public class TransitionUI : UIBase<TransitionUIData>, ITransitionUI
{
    [SerializeField] private float _fadeDuration = 0.5f;

    private CanvasGroup _canvasGroup;
    private bool _debugEnabled;

    protected override void OnInit()
    {
        base.OnInit();
        _canvasGroup = GetComponent<CanvasGroup>();
        _canvasGroup.alpha = 0f;
    }

    public override void OnOpen()
    {
        _debugEnabled = ConfigComponent.Instance.Get<GameConfig>().EnableDebug;
        UI.Debug.GameObject.SetActive(_debugEnabled);
        SetStatus("Loading", string.Empty, 0f);
    }

    public override void OnClose()
    {
    }

    public void SetStatus(string title, string detail, float progress)
    {
        UI.BarMask_Bar.Image.fillAmount = Mathf.Clamp01(progress);

        if (!_debugEnabled)
            return;

        UI.Debug_LoadingTitle.TextMeshProUGUI.text = string.IsNullOrWhiteSpace(title) ? "Loading" : title;
        UI.Debug_LoadingDetail.TextMeshProUGUI.text = detail ?? string.Empty;
    }

    public IEnumerator Show()
    {
        float elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _fadeDuration);
            _canvasGroup.alpha = 1f - Mathf.Pow(1f - t, 3f);
            yield return null;
        }

        _canvasGroup.alpha = 1f;
    }

    public IEnumerator Hide()
    {
        float elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _fadeDuration);
            _canvasGroup.alpha = 1f - Mathf.Pow(t, 3f);
            yield return null;
        }

        _canvasGroup.alpha = 0f;
    }
}
