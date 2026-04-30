using System.Collections;
using CrystalMagic.Core;
using UnityEngine;

public class TransitionMaskUI : UIBase<TransitionMaskUIData>, ITransitionUI
{
    private CanvasGroup _canvasGroup;
    [SerializeField] private float _fadeDuration = 0.5f;

    protected override void OnInit()
    {
        base.OnInit();

        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        _canvasGroup.alpha = 0f;
    }

    public override void OnOpen()
    {
    }

    public override void OnClose()
    {
    }

    public IEnumerator Show()
    {
        float elapsed = 0f;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / _fadeDuration);
            _canvasGroup.alpha = t;
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
            _canvasGroup.alpha = 1f - t;
            yield return null;
        }

        _canvasGroup.alpha = 0f;
    }
}
