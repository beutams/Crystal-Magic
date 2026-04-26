// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class EffectSelectUIData : UIData
{
    public UINode ScrollView;
    public UINode ScrollView_Viewport;
    public UINode ScrollView_Viewport_Content;
    public UINode ScrollView_Viewport_Content_Item;
    public UINode ScrollView_Viewport_Content_Item_Background;
    public UINode ScrollView_Viewport_Content_Item_Icon;
    public UINode ScrollView_Viewport_Content_Item_Name;

    public override void Bind(Transform root)
    {
        ScrollView = UINode.From(Find(root, "Scroll View"));
        ScrollView_Viewport = UINode.From(Find(root, "Scroll View/Viewport"));
        ScrollView_Viewport_Content = UINode.From(Find(root, "Scroll View/Viewport/Content"));
        ScrollView_Viewport_Content_Item = UINode.From(Find(root, "Scroll View/Viewport/Content/Item"));
        ScrollView_Viewport_Content_Item_Background = UINode.From(Find(root, "Scroll View/Viewport/Content/Item/Background"));
        ScrollView_Viewport_Content_Item_Icon = UINode.From(Find(root, "Scroll View/Viewport/Content/Item/Icon"));
        ScrollView_Viewport_Content_Item_Name = UINode.From(Find(root, "Scroll View/Viewport/Content/Item/Name"));
    }
}
