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
        UI.Fog.Image.sprite = null;
        base.OnClose();
    }

    protected override void RefreshView()
    {
        bool hasMap = Model != null && Model.HasMap;
        UI.Panel.GameObject.SetActive(hasMap);
        if (!hasMap)
        {
            UI.Terrain.Image.enabled = false;
            UI.Fog.GameObject.SetActive(false);
            ClearInterestPointViews();
            return;
        }

        UI.Terrain.Image.enabled = true;
        UI.Terrain.Image.sprite = Model.TerrainSprite;
        UI.Fog.GameObject.SetActive(Model.HasFog);
        UI.Fog.Image.sprite = Model.FogSprite;
        RenderInterestPoints();
        RenderMarker(UI.Exit, Model.HasExit && Model.IsExitExplored, Model.ExitPosition, 0f);
        RenderMarker(UI.Player, Model.HasPlayer, Model.PlayerPosition, Model.PlayerRotationDegrees);
    }

    private void RenderInterestPoints()
    {
        OpenFieldDungeonLayout layout = Model.Layout;
        if (!ReferenceEquals(_renderedInterestPointLayout, layout))
        {
            ClearInterestPointViews();
            _renderedInterestPointLayout = layout;
            if (layout != null && layout.InterestPoints.Count > 0)
            {
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
        }

        if (layout == null)
            return;

        for (int index = 0; index < _interestPointViews.Count; index++)
        {
            OpenFieldInterestPoint point = layout.InterestPoints[index];
            _interestPointViews[index].gameObject.SetActive(Model.IsCellExplored(point.Center.X, point.Center.Y));
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
