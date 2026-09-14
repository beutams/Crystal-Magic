// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Right-click Prefab -> Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class SettingUIData : UIData
{
    public UINode Overlay;
    public UINode Panel;
    public UINode Panel_Title;
    public UINode Panel_RowsLabel;
    public UINode Panel_RowsValue;
    public UINode Panel_MasterVolumeSlider;
    public UINode Panel_BgmVolumeSlider;
    public UINode Panel_SfxVolumeSlider;
    public UINode Panel_LanguagePrevious;
    public UINode Panel_LanguageNext;
    public UINode Panel_Save;
    public UINode Panel_Reset;
    public UINode Panel_Back;

    public override void Bind(Transform root)
    {
        Overlay = UINode.From(Find(root, "Overlay"));
        Panel = UINode.From(Find(root, "Panel"));
        Panel_Title = UINode.From(Find(root, "Panel/Title"));
        Panel_RowsLabel = UINode.From(Find(root, "Panel/RowsLabel"));
        Panel_RowsValue = UINode.From(Find(root, "Panel/RowsValue"));
        Panel_MasterVolumeSlider = UINode.From(Find(root, "Panel/MasterVolumeSlider"));
        Panel_BgmVolumeSlider = UINode.From(Find(root, "Panel/BgmVolumeSlider"));
        Panel_SfxVolumeSlider = UINode.From(Find(root, "Panel/SfxVolumeSlider"));
        Panel_LanguagePrevious = UINode.From(Find(root, "Panel/LanguagePrevious"));
        Panel_LanguageNext = UINode.From(Find(root, "Panel/LanguageNext"));
        Panel_Save = UINode.From(Find(root, "Panel/Save"));
        Panel_Reset = UINode.From(Find(root, "Panel/Reset"));
        Panel_Back = UINode.From(Find(root, "Panel/Back"));
    }
}
