using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.UI;
using UnityEngine;
using UnityEngine.UI;

public class InteractionSelectUI : UIBase<InteractionSelectUIData, InteractionSelectUIModel>
{
    private const float HorizontalPadding = 40f;
    private const float DialogInset = 68f;
    private const float TopPadding = 54f;
    private const float BottomPadding = 34f;
    private const float DialogOptionSpacing = 44f;

    private readonly List<InteractionSelectUI_OptionView> _optionViews = new();

    public event Action<InteractionSelectOptionDisplayData> OptionClicked;

    public override void OnClose()
    {
        UISubViewBase.ReleaseAllToPool(_optionViews);
        base.OnClose();
    }

    protected override void RefreshView()
    {
        UI.Dialog.TextMeshProUGUI.text = Model.Dialog;
        RenderOptions(Model.Options);
        RefreshLayout(_optionViews.Count);
    }

    private void RefreshLayout(int optionCount)
    {
        float panelWidth = UI.BG.RectTransform.sizeDelta.x;
        float rowHeight = UI.Content_Button.RectTransform.sizeDelta.y;
        float rowSpacing = UI.Content.GameObject.GetComponent<VerticalLayoutGroup>().spacing;
        float contentHeight = optionCount * rowHeight + Mathf.Max(0, optionCount - 1) * rowSpacing;
        float dialogWidth = panelWidth - DialogInset * 2f;
        bool hasDialog = !string.IsNullOrWhiteSpace(UI.Dialog.TextMeshProUGUI.text);
        float dialogHeight = hasDialog
            ? Mathf.Ceil(UI.Dialog.TextMeshProUGUI.GetPreferredValues(
                UI.Dialog.TextMeshProUGUI.text, dialogWidth, Mathf.Infinity).y)
            : 0f;
        float dialogSpacing = hasDialog && optionCount > 0 ? DialogOptionSpacing : 0f;
        float panelHeight = TopPadding + dialogHeight + dialogSpacing + contentHeight + BottomPadding;

        UI.BG.RectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight);
        UI.Content.GameObject.SetActive(optionCount > 0);
        UI.Content.RectTransform.sizeDelta = new Vector2(panelWidth - HorizontalPadding * 2f, contentHeight);
        UI.Content.RectTransform.anchoredPosition = new Vector2(-HorizontalPadding, BottomPadding);
        UI.Dialog.GameObject.SetActive(hasDialog);
        UI.Dialog.RectTransform.sizeDelta = new Vector2(dialogWidth, dialogHeight);
        UI.Dialog.RectTransform.anchoredPosition = new Vector2(-panelWidth + DialogInset, panelHeight - TopPadding);

        if (optionCount > 0)
            LayoutRebuilder.ForceRebuildLayoutImmediate(UI.Content.RectTransform);
    }

    private void RenderOptions(IReadOnlyList<InteractionSelectOptionDisplayData> options)
    {
        int optionCount = options != null ? options.Count : 0;
        EnsureOptionViews(optionCount);

        for (int i = 0; i < _optionViews.Count; i++)
        {
            InteractionSelectOptionDisplayData option = options != null && i < options.Count ? options[i] : null;
            _optionViews[i].Render(option);
        }
    }

    private void EnsureOptionViews(int optionCount)
    {
        UI.Content_Button.GameObject.SetActive(false);

        while (_optionViews.Count > optionCount)
        {
            int lastIndex = _optionViews.Count - 1;
            InteractionSelectUI_OptionView optionView = _optionViews[lastIndex];
            UISubViewBase.ReleaseToPool(optionView);
            _optionViews.RemoveAt(lastIndex);
        }

        InteractionSelectUI_OptionView templateView = UI.Content_Button.GameObject.GetComponent<InteractionSelectUI_OptionView>();
        UISubViewBase.EnsurePoolCapacity(templateView, optionCount, optionCount);

        while (_optionViews.Count < optionCount)
        {
            InteractionSelectUI_OptionView optionView = UISubViewBase.AcquireFromPool(
                templateView,
                UI.Content.GameObject.transform);
            BindOptionView(optionView);
            _optionViews.Add(optionView);
        }
    }

    private void BindOptionView(InteractionSelectUI_OptionView optionView)
    {
        optionView.Clicked -= HandleOptionClicked;
        optionView.Clicked += HandleOptionClicked;
    }

    private void HandleOptionClicked(InteractionSelectOptionDisplayData option)
    {
        OptionClicked?.Invoke(option);
    }
}
