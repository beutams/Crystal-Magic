using CrystalMagic.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TransitionUI : UIBase<TransitionUIData>
{
    private const string TitleNodeName = "LoadingTitle";
    private const string DetailNodeName = "LoadingDetail";
    private const string ProgressBackgroundNodeName = "LoadingProgressBackground";
    private const string ProgressFillNodeName = "LoadingProgressFill";

    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _detailText;
    private Image _progressFillImage;

    protected override void OnInit()
    {
        base.OnInit();
        EnsureRuntimeWidgets();
    }

    public override void OnOpen()
    {
        SetStatus("Loading", string.Empty, 0f);
    }

    public override void OnClose()
    {
    }

    public void SetStatus(string title, string detail, float progress)
    {
        if (_titleText != null)
            _titleText.text = string.IsNullOrWhiteSpace(title) ? "Loading" : title;
        if (_detailText != null)
            _detailText.text = detail ?? string.Empty;
        if (_progressFillImage != null)
            _progressFillImage.fillAmount = Mathf.Clamp01(progress);
    }

    private void EnsureRuntimeWidgets()
    {
        RectTransform container = UI?.Image?.RectTransform ?? transform as RectTransform;
        if (container == null)
            return;

        TMP_FontAsset fontAsset = TMP_Settings.defaultFontAsset;
        if (fontAsset == null)
            fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        _titleText = GetOrCreateText(container, TitleNodeName, fontAsset, 34f, FontStyles.Bold, new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), Vector2.zero, new Vector2(760f, 60f));
        _detailText = GetOrCreateText(container, DetailNodeName, fontAsset, 24f, FontStyles.Normal, new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f), Vector2.zero, new Vector2(900f, 120f));

        RectTransform progressBackground = GetOrCreateRect(container, ProgressBackgroundNodeName, new Vector2(0.5f, 0.43f), new Vector2(0.5f, 0.43f), Vector2.zero, new Vector2(720f, 24f));
        Image backgroundImage = progressBackground.GetComponent<Image>();
        if (backgroundImage == null)
            backgroundImage = progressBackground.gameObject.AddComponent<Image>();
        backgroundImage.color = new Color(1f, 1f, 1f, 0.16f);
        backgroundImage.raycastTarget = false;

        RectTransform progressFill = GetOrCreateRect(progressBackground, ProgressFillNodeName, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        _progressFillImage = progressFill.GetComponent<Image>();
        if (_progressFillImage == null)
            _progressFillImage = progressFill.gameObject.AddComponent<Image>();
        _progressFillImage.color = new Color(0.34f, 0.76f, 1f, 0.96f);
        _progressFillImage.type = Image.Type.Filled;
        _progressFillImage.fillMethod = Image.FillMethod.Horizontal;
        _progressFillImage.fillOrigin = 0;
        _progressFillImage.fillAmount = 0f;
        _progressFillImage.raycastTarget = false;
    }

    private static TextMeshProUGUI GetOrCreateText(
        RectTransform parent,
        string nodeName,
        TMP_FontAsset fontAsset,
        float fontSize,
        FontStyles fontStyle,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        RectTransform rectTransform = GetOrCreateRect(parent, nodeName, anchorMin, anchorMax, anchoredPosition, sizeDelta);
        TextMeshProUGUI text = rectTransform.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = rectTransform.gameObject.AddComponent<TextMeshProUGUI>();

        if (fontAsset != null)
            text.font = fontAsset;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform GetOrCreateRect(
        RectTransform parent,
        string nodeName,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        Transform existing = parent.Find(nodeName);
        RectTransform rectTransform;
        if (existing == null)
        {
            GameObject node = new GameObject(nodeName, typeof(RectTransform));
            rectTransform = node.GetComponent<RectTransform>();
            rectTransform.SetParent(parent, false);
        }
        else
        {
            rectTransform = existing as RectTransform;
        }

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        return rectTransform;
    }
}
