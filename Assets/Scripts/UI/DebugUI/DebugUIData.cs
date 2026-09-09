// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Right-click Prefab -> Assets/Tools/Generate UIData to regenerate

using CrystalMagic.Core;
using UnityEngine;

public class DebugUIData : UIData
{
    public UINode Launcher;
    public UINode Launcher_Label;
    public UINode Content;
    public UINode Content_Title;
    public UINode Content_Close;
    public UINode Content_Navigation;
    public UINode Content_Navigation_Viewport;
    public UINode Content_Navigation_Viewport_Content;
    public UINode Content_Navigation_Viewport_Content_DebugItem;
    public UINode Content_PlayerAttributes;
    public UINode Content_PlayerAttributes_Title;
    public UINode Content_PlayerAttributes_Value;
    public UINode Content_TrainingGround;
    public UINode Content_TrainingGround_Title;
    public UINode Content_TrainingGround_Value;

    public override void Bind(Transform root)
    {
        Launcher = UINode.From(Find(root, "Launcher"));
        Launcher_Label = UINode.From(Find(root, "Launcher/Label"));
        Content = UINode.From(Find(root, "Content"));
        Content_Title = UINode.From(Find(root, "Content/Title"));
        Content_Close = UINode.From(Find(root, "Content/Close"));
        Content_Navigation = UINode.From(Find(root, "Content/Navigation"));
        Content_Navigation_Viewport = UINode.From(Find(root, "Content/Navigation/Viewport"));
        Content_Navigation_Viewport_Content = UINode.From(Find(root, "Content/Navigation/Viewport/Content"));
        Content_Navigation_Viewport_Content_DebugItem = UINode.From(Find(root, "Content/Navigation/Viewport/Content/DebugItem"));
        Content_PlayerAttributes = UINode.From(Find(root, "Content/PlayerAttributes"));
        Content_PlayerAttributes_Title = UINode.From(Find(root, "Content/PlayerAttributes/Title"));
        Content_PlayerAttributes_Value = UINode.From(Find(root, "Content/PlayerAttributes/Value"));
        Content_TrainingGround = UINode.From(Find(root, "Content/TrainingGround"));
        Content_TrainingGround_Title = UINode.From(Find(root, "Content/TrainingGround/Title"));
        Content_TrainingGround_Value = UINode.From(Find(root, "Content/TrainingGround/Value"));
    }
}
