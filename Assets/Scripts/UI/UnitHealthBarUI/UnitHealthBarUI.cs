using CrystalMagic.Core;
using UnityEngine;
using UnityEngine.UI;

public class UnitHealthBarUI : UIBase<UnitHealthBarUIData>
{
    private RectTransform _rectTransform;
    private Canvas _canvas;
    private GraphicRaycaster _raycaster;
    private float _hpMaskBaseWidth = -1f;

    protected override void OnInit()
    {
        base.OnInit();
        _rectTransform = transform as RectTransform;
        _canvas = GetComponent<Canvas>();
        _raycaster = GetComponent<GraphicRaycaster>();
        CacheBarWidth();
    }

    public override void OnOpen()
    {
    }

    public override void OnClose()
    {
    }

    public void PrepareForFloatingRoot(Transform parent)
    {
        EnsureInitialized();

        _rectTransform ??= transform as RectTransform;
        if (_rectTransform == null)
            return;

        _rectTransform.SetParent(parent, false);
        _rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        _rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        _rectTransform.pivot = new Vector2(0.5f, 0.5f);
        _rectTransform.anchoredPosition = Vector2.zero;
        _rectTransform.localScale = Vector3.one;
        _rectTransform.localRotation = Quaternion.identity;

        if (_canvas != null)
            _canvas.enabled = false;

        if (_raycaster != null)
            _raycaster.enabled = false;
    }

    public void SetHealth(float currentHealth, float maxHealth)
    {
        CacheBarWidth();
        if (UI?.HP_BarMask.RectTransform == null || _hpMaskBaseWidth <= 0f)
            return;

        float ratio = maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
        UI.HP_BarMask.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _hpMaskBaseWidth * ratio);
    }

    public void SetAnchoredPosition(Vector2 anchoredPosition)
    {
        _rectTransform ??= transform as RectTransform;
        if (_rectTransform != null)
            _rectTransform.anchoredPosition = anchoredPosition;
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible)
            gameObject.SetActive(visible);
    }

    private void CacheBarWidth()
    {
        if (_hpMaskBaseWidth > 0f || UI?.HP_BarMask.RectTransform == null)
            return;

        _hpMaskBaseWidth = UI.HP_BarMask.RectTransform.rect.width;
        if (_hpMaskBaseWidth <= 0f)
            _hpMaskBaseWidth = UI.HP_BarMask.RectTransform.sizeDelta.x;
    }
}
