using System.Collections;
using CrystalMagic.Core;
using UnityEngine;

public class TransitionUI : UIBase, ITransitionUI
{
    private CanvasGroup _canvasGroup;
    [SerializeField] private float _fadeDuration = 0.5f;

    protected override void OnInit()
    {
        // ��ȡ�򴴽� CanvasGroup
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        // ��ʼ״̬Ϊ͸��
        _canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// ��ʾת�����棨���룩
    /// </summary>
    public IEnumerator Show()
    {
        float elapsed = 0f;
        
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _fadeDuration);
            _canvasGroup.alpha = t;
            yield return null;
        }
        
        _canvasGroup.alpha = 1f;
    }

    /// <summary>
    /// ����ת�����棨������
    /// </summary>
    public IEnumerator Hide()
    {
        float elapsed = 0f;
        
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _fadeDuration);
            _canvasGroup.alpha = 1f - t;
            yield return null;
        }
        
        _canvasGroup.alpha = 0f;
    }
}
