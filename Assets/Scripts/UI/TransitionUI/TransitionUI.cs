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
        // FadeOutStarted synchronously enters the target state.  That state's first
        // frame can build the map and UI, so discard it instead of treating its long
        // unscaled delta time as an entire fade animation.
        _canvasGroup.alpha = 1f;
        yield return null;

        float fadeStartTime = Time.unscaledTime;
        while (Time.unscaledTime - fadeStartTime < _fadeDuration)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - fadeStartTime) / _fadeDuration);
            _canvasGroup.alpha = 1f - Mathf.Pow(t, 3f);
            yield return null;
        }

        _canvasGroup.alpha = 0f;
    }
}
