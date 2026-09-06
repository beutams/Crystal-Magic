using CrystalMagic.Core;
using UnityEngine;

public sealed class MinimapInterestPointView : UISubView<MinimapInterestPointData>
{
    public void Render(Vector2 anchorMin, Vector2 anchorMax)
    {
        Rebind();

        RectTransform rectTransform = transform as RectTransform;
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localRotation = Quaternion.identity;
    }
}
