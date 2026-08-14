using System;
using CrystalMagic.Core;
using CrystalMagic.UI;

public class TrainingDebugUI_UnitItemView : UISubView<TrainingDebugUI_UnitItemData>
{
    private EntitySelection _selection;

    public event Action<EntitySelection> Selected;

    public void Render(TrainingDebugUnitSnapshot snapshot)
    {
        _selection = new EntitySelection(snapshot.Entity.Index, snapshot.Entity.Version);
        UI.Button.ButtonPlus.onClick.RemoveListener(OnClicked);
        UI.Button.ButtonPlus.onClick.AddListener(OnClicked);
        UI.Label.TextMeshProUGUI.text = $"{snapshot.UnitName}\n{snapshot.StateName} | HP {snapshot.CurrentHealth:0}/{snapshot.MaxHealth:0} | {(snapshot.HasAI ? "AI" : "No AI")}";
        UI.Button.Image.color = snapshot.IsSelected
            ? new UnityEngine.Color(0.25f, 0.56f, 0.76f, 0.95f)
            : new UnityEngine.Color(0.12f, 0.16f, 0.2f, 0.95f);
    }

    private void OnClicked()
    {
        Selected?.Invoke(_selection);
    }
}
