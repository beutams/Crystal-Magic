using System.Collections;
using CrystalMagic.Core;
using UnityEngine;

public class TransitionUI : UIBase, ITransitionUI
{
    private CanvasGroup _canvasGroup;
    [SerializeField] private float _fadeDuration = 0.5f;

    protected override void OnInit()
    {
        // 获取或创建 CanvasGroup
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // 初始状态为透明
        _canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// 显示转场界面（淡入）
    /// </summary>
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

    /// <summary>
    /// 隐藏转场界面（淡出）
    /// </summary>
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
