using CrystalMagic.Core;
using UnityEngine;

public sealed class MinimapInterestPointView : UISubView<MinimapInterestPointData>
{
    [SerializeField] private Color _activeColor = new Color32(132, 183, 172, 255);
    [SerializeField] private Color _clearedColor = new Color32(137, 137, 137, 255);

    public void Render(Vector2 normalizedPosition, bool isCleared)
    {
        RectTransform rectTransform = (RectTransform)transform;
        rectTransform.anchorMin = normalizedPosition;
        rectTransform.anchorMax = normalizedPosition;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localRotation = Quaternion.identity;
        UI.Icon.Image.color = isCleared ? _clearedColor : _activeColor;
    }
}
