using CrystalMagic.Core;
using CrystalMagic.UI;
using UnityEngine;

public sealed class GuideUI : UIBase<GuideUIData, GuideUIModel>
{
    public RectTransform Root => (RectTransform)transform;
    protected override void RefreshView()
    {
        UI.Mask.GameObject.GetComponent<GuideMaskGraphic>().Render(Model.Holes, Model.BlockOutside);
        UI.Hint_Label.TextMeshProUGUI.text = Model.Text;
        Rect hintBounds = new(-725, Root.rect.yMin + 32, 1450, 145);
        bool overlap = false;
        foreach (Rect hole in Model.Holes) if (hole.Overlaps(hintBounds)) overlap = true;
        UI.Hint.RectTransform.anchoredPosition = new Vector2(0, overlap ? Root.rect.height - 180 : 32);
    }
}
