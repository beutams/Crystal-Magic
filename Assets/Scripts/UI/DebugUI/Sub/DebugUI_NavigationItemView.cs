using System;
using CrystalMagic.Core;
using CrystalMagic.UI;
using UnityEngine;

public sealed class DebugUI_NavigationItemView : UISubView<DebugUI_NavigationItemData>
{
    private DebugPage _page;

    public event Action<DebugPage> PageRequested;

    protected override void Awake()
    {
        base.Awake();
        UI.Root.ButtonPlus.onClick.AddListener(HandleClicked);
    }

    public void Render(DebugPageDefinition definition, bool isSelected)
    {
        Rebind();
        _page = definition.Page;
        UI.Label.TextMeshProUGUI.text = definition.Title;
        UI.Root.Image.color = isSelected
            ? new Color(0.2f, 0.56f, 0.68f, 0.96f)
            : new Color(0.1f, 0.16f, 0.2f, 0.92f);
    }

    private void HandleClicked()
    {
        PageRequested?.Invoke(_page);
    }
}
