using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.UI;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed class DebugUI : UIBase<DebugUIData, DebugUIModel>
{
    private const string LauncherPositionXKey = "CrystalMagic.DebugUI.LauncherX";
    private const string LauncherPositionYKey = "CrystalMagic.DebugUI.LauncherY";
    private const float LauncherEdgePadding = 12f;

    private readonly List<DebugUI_NavigationItemView> _pageItemViews = new();
    private Vector2 _launcherPointerOffset;

    public event Action ContentToggleRequested;
    public event Action ContentHideRequested;
    public event Action<DebugPage> PageRequested;

    protected override void OnInit()
    {
        base.OnInit();
        UI.Content_Navigation_Viewport_Content_DebugItem.GameObject.SetActive(false);
        UI.Content.GameObject.SetActive(false);
    }

    public override void OnOpen()
    {
        UI.Launcher.ButtonPlus.onClick.AddListener(HandleLauncherClicked);
        UI.Content_Close.ButtonPlus.onClick.AddListener(HandleContentHideClicked);

        DebugUI_LauncherDrag launcherDrag = UI.Launcher.GameObject.GetComponent<DebugUI_LauncherDrag>();
        launcherDrag.DragStarted += HandleLauncherDragStarted;
        launcherDrag.Dragging += HandleLauncherDragging;
        launcherDrag.DragEnded += HandleLauncherDragEnded;

        RestoreLauncherPosition();
        base.OnOpen();
    }

    public override void OnClose()
    {
        UI.Launcher.ButtonPlus.onClick.RemoveListener(HandleLauncherClicked);
        UI.Content_Close.ButtonPlus.onClick.RemoveListener(HandleContentHideClicked);

        DebugUI_LauncherDrag launcherDrag = UI.Launcher.GameObject.GetComponent<DebugUI_LauncherDrag>();
        launcherDrag.DragStarted -= HandleLauncherDragStarted;
        launcherDrag.Dragging -= HandleLauncherDragging;
        launcherDrag.DragEnded -= HandleLauncherDragEnded;

        ReleasePageItems();
        base.OnClose();
    }

    protected override void RefreshView()
    {
        SetContentVisible(Model.IsContentVisible);
        RenderPageItems(Model.Pages);

        UI.Content_PlayerAttributes.GameObject.SetActive(Model.SelectedPage == DebugPage.PlayerAttributes);
        UI.Content_TrainingGround.GameObject.SetActive(Model.SelectedPage == DebugPage.TrainingGround);
        UI.Content_PlayerAttributes_Value.TextMeshProUGUI.text = Model.PlayerAttributesText;
        UI.Content_TrainingGround_Value.TextMeshProUGUI.text = Model.TrainingGroundText;
    }

    private void SetContentVisible(bool visible)
    {
        UI.Content.GameObject.SetActive(visible);
        UI.Content.CanvasGroup.alpha = 1f;
        UI.Content.CanvasGroup.interactable = visible;
        UI.Content.CanvasGroup.blocksRaycasts = visible;

        if (!visible)
            EventSystem.current?.SetSelectedGameObject(null);
    }

    private void RenderPageItems(IReadOnlyList<DebugPageDefinition> pages)
    {
        EnsurePageItems(pages.Count);
        for (int i = 0; i < _pageItemViews.Count; i++)
            _pageItemViews[i].Render(pages[i], pages[i].Page == Model.SelectedPage);
    }

    private void EnsurePageItems(int itemCount)
    {
        UI.Content_Navigation_Viewport_Content_DebugItem.GameObject.SetActive(false);

        while (_pageItemViews.Count > itemCount)
        {
            int lastIndex = _pageItemViews.Count - 1;
            ReleasePageItem(_pageItemViews[lastIndex]);
            _pageItemViews.RemoveAt(lastIndex);
        }

        DebugUI_NavigationItemView templateView = UI.Content_Navigation_Viewport_Content_DebugItem.GameObject.GetComponent<DebugUI_NavigationItemView>();
        UISubViewBase.EnsurePoolCapacity(templateView, itemCount, itemCount);

        while (_pageItemViews.Count < itemCount)
        {
            DebugUI_NavigationItemView itemView = UISubViewBase.AcquireFromPool(
                templateView,
                UI.Content_Navigation_Viewport_Content.GameObject.transform);
            itemView.gameObject.SetActive(true);
            itemView.PageRequested += HandlePageRequested;
            _pageItemViews.Add(itemView);
        }
    }

    private void ReleasePageItems()
    {
        for (int i = _pageItemViews.Count - 1; i >= 0; i--)
            ReleasePageItem(_pageItemViews[i]);

        _pageItemViews.Clear();
    }

    private void ReleasePageItem(DebugUI_NavigationItemView itemView)
    {
        itemView.PageRequested -= HandlePageRequested;
        UISubViewBase.ReleaseToPool(itemView);
    }

    private void HandleLauncherClicked()
    {
        if (UI.Launcher.GameObject.GetComponent<DebugUI_LauncherDrag>().ConsumeDrag())
            return;

        ContentToggleRequested?.Invoke();
    }

    private void HandleContentHideClicked()
    {
        ContentHideRequested?.Invoke();
    }

    private void HandlePageRequested(DebugPage page)
    {
        PageRequested?.Invoke(page);
    }

    private void HandleLauncherDragStarted(PointerEventData eventData)
    {
        _launcherPointerOffset = UI.Launcher.RectTransform.anchoredPosition - GetRootLocalPointerPosition(eventData);
    }

    private void HandleLauncherDragging(PointerEventData eventData)
    {
        SetLauncherPosition(GetRootLocalPointerPosition(eventData) + _launcherPointerOffset);
    }

    private void HandleLauncherDragEnded(PointerEventData eventData)
    {
        SetLauncherPosition(GetRootLocalPointerPosition(eventData) + _launcherPointerOffset);
        PlayerPrefs.SetFloat(LauncherPositionXKey, UI.Launcher.RectTransform.anchoredPosition.x);
        PlayerPrefs.SetFloat(LauncherPositionYKey, UI.Launcher.RectTransform.anchoredPosition.y);
        PlayerPrefs.Save();
    }

    private Vector2 GetRootLocalPointerPosition(PointerEventData eventData)
    {
        Camera eventCamera = Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            transform as RectTransform,
            eventData.position,
            eventCamera,
            out Vector2 localPosition);
        return localPosition;
    }

    private void RestoreLauncherPosition()
    {
        if (!PlayerPrefs.HasKey(LauncherPositionXKey))
        {
            SetLauncherPosition(UI.Launcher.RectTransform.anchoredPosition);
            return;
        }

        SetLauncherPosition(new Vector2(
            PlayerPrefs.GetFloat(LauncherPositionXKey),
            PlayerPrefs.GetFloat(LauncherPositionYKey)));
    }

    private void SetLauncherPosition(Vector2 position)
    {
        Rect rootRect = (transform as RectTransform).rect;
        Vector2 halfSize = UI.Launcher.RectTransform.rect.size * 0.5f;
        position.x = Mathf.Clamp(position.x, rootRect.xMin + halfSize.x + LauncherEdgePadding, rootRect.xMax - halfSize.x - LauncherEdgePadding);
        position.y = Mathf.Clamp(position.y, rootRect.yMin + halfSize.y + LauncherEdgePadding, rootRect.yMax - halfSize.y - LauncherEdgePadding);
        UI.Launcher.RectTransform.anchoredPosition = position;
    }
}
