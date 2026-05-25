using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.UI;

public class InteractionSelectUI : UIBase<InteractionSelectUIData, InteractionSelectUIModel>
{
    private readonly List<InteractionSelectUI_OptionView> _optionViews = new();

    public event Action<InteractionSelectOptionDisplayData> OptionClicked;

    protected override void OnInit()
    {
        base.OnInit();
    }

    public override void OnOpen()
    {
        base.OnOpen();
    }

    public override void OnClose()
    {
        UISubViewBase.ReleaseAllToPool(_optionViews);
        base.OnClose();
    }

    protected override void RefreshView()
    {
        RenderOptions(Model.Options);
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
