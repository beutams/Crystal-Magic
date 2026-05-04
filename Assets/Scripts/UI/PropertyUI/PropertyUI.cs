using CrystalMagic.Core;
using CrystalMagic.UI;

public class PropertyUI : UIBase<PropertyUIData, PropertyUIModel>
{
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
        base.OnClose();
    }

    public override void OnUpdate()
    {
        base.OnUpdate();
        if (Model != null)
            Model.RefreshRuntime();
    }

    protected override void RefreshView()
    {
        SetValue(UI.Speed_Value, FormatNumber(Model != null ? Model.Speed : 0f));
        SetValue(UI.MaxHealth_Value, FormatNumber(Model != null ? Model.MaxHealth : 0f));
        SetValue(UI.HealthRegen_Value, FormatNumber(Model != null ? Model.HealthRegen : 0f));
        SetValue(UI.MaxMana_Value, FormatNumber(Model != null ? Model.MaxMana : 0f));
        SetValue(UI.ManaRegen_Value, FormatNumber(Model != null ? Model.ManaRegen : 0f));
        SetValue(UI.AttackPower_Value, FormatNumber(Model != null ? Model.AttackPower : 0f));
        SetValue(UI.ActionSpeed_Value, FormatPercent(Model != null ? Model.ActionSpeed : 0f));
        SetValue(UI.ChantSpeed_Value, FormatPercent(Model != null ? Model.ChantSpeed : 0f));
        SetValue(UI.Fire_Value, FormatPercent(Model != null ? Model.Fire : 0f));
        SetValue(UI.Water_Value, FormatPercent(Model != null ? Model.Water : 0f));
        SetValue(UI.Lighting_Value, FormatPercent(Model != null ? Model.Lighting : 0f));
        SetValue(UI.Wind_Value, FormatPercent(Model != null ? Model.Wind : 0f));
        SetValue(UI.SkillRange_Value, FormatNumber(Model != null ? Model.SkillRange : 0f));
    }

    private static void SetValue(UINode node, string value)
    {
        if (node?.TextMeshProUGUI != null)
            node.TextMeshProUGUI.text = value;
    }

    private static string FormatNumber(float value)
    {
        return value.ToString("0.##");
    }

    private static string FormatPercent(float value)
    {
        return $"{value * 100f:+0.##;-0.##;0}%";
    }
}
