using CrystalMagic.Core;
using CrystalMagic.Game.OpenField;
using UnityEngine;

public sealed class MinimapInterestPointView : UISubView<MinimapInterestPointData>
{
    [SerializeField] private Color _smallColor = new Color32(111, 158, 185, 255);
    [SerializeField] private Color _mediumColor = new Color32(199, 164, 93, 255);
    [SerializeField] private Color _largeColor = new Color32(189, 115, 103, 255);
    [SerializeField] private Color _clearedColor = new Color32(137, 137, 137, 255);

    public void Render(Vector2 normalizedPosition, OpenFieldInterestSize size, bool isCleared)
    {
        RectTransform rectTransform = (RectTransform)transform;
        rectTransform.anchorMin = normalizedPosition;
        rectTransform.anchorMax = normalizedPosition;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localRotation = Quaternion.identity;
        UI.Icon.Image.color = isCleared ? _clearedColor : size switch
        {
            OpenFieldInterestSize.Medium => _mediumColor,
            OpenFieldInterestSize.Large => _largeColor,
            _ => _smallColor,
        };
    }

    public void RenderSquad(Vector2 normalizedPosition)
    {
        Render(normalizedPosition, OpenFieldInterestSize.Large, false);
        UI.Icon.Image.color = new Color32(239, 82, 69, 255);
    }
}
