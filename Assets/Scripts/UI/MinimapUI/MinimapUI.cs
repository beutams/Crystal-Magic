using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.OpenField;
using CrystalMagic.UI;
using UnityEngine;

public sealed class MinimapUI : UIBase<MinimapUIData, MinimapUIModel>
{
    private readonly List<MinimapInterestPointView> _interestPointViews = new();
    private OpenFieldDungeonLayout _renderedInterestPointLayout;

    public override void OnClose()
    {
        ClearInterestPointViews();
        UI.Terrain.Image.sprite = null;
        base.OnClose();
    }

    protected override void RefreshView()
    {
        bool hasMap = Model != null && Model.HasMap;
        UI.Panel.GameObject.SetActive(hasMap);
        if (!hasMap)
        {
            ClearInterestPointViews();
            return;
        }

        UI.Terrain.Image.sprite = Model.TerrainSprite;
        RenderInterestPoints();
        RenderMarker(UI.Exit, Model.HasExit, Model.ExitPosition, 0f);
        RenderMarker(UI.Player, Model.HasPlayer, Model.PlayerPosition, Model.PlayerRotationDegrees);
    }

    private void RenderInterestPoints()
    {
        OpenFieldDungeonLayout layout = Model.Layout;
        if (ReferenceEquals(_renderedInterestPointLayout, layout))
            return;

        ClearInterestPointViews();
        _renderedInterestPointLayout = layout;
        if (layout == null || layout.InterestPoints.Count == 0)
            return;

        UISubViewBase.EnsurePoolCapacity(UI.InterestPointTemplate, layout.InterestPoints.Count);
        for (int index = 0; index < layout.InterestPoints.Count; index++)
        {
            MinimapInterestPointView view = UISubViewBase.AcquireFromPool(
                UI.InterestPointTemplate,
                UI.InterestPointRoot.RectTransform);
            Model.GetInterestPointAnchorRange(layout.InterestPoints[index], out Vector2 anchorMin, out Vector2 anchorMax);
            view.Render(anchorMin, anchorMax);
            _interestPointViews.Add(view);
        }
    }

    private void ClearInterestPointViews()
    {
        for (int index = _interestPointViews.Count - 1; index >= 0; index--)
            UISubViewBase.ReleaseToPool(_interestPointViews[index]);

        _interestPointViews.Clear();
        _renderedInterestPointLayout = null;
    }

    private static void RenderMarker(UINode marker, bool isVisible, Vector2 normalizedPosition, float rotationDegrees)
    {
        marker.GameObject.SetActive(isVisible);
        if (!isVisible)
            return;

        RectTransform rectTransform = marker.RectTransform;
        rectTransform.anchorMin = normalizedPosition;
        rectTransform.anchorMax = normalizedPosition;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotationDegrees);
    }
}
