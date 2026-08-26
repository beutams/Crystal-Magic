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
    public UINode ScrollView_Viewport_Content_Item_IconBG;
    public UINode ScrollView_Viewport_Content_Item_IconBG_Mask;
    public UINode ScrollView_Viewport_Content_Item_IconBG_Mask_Icon;
    public UINode ScrollView_Viewport_Content_Item_Name;

    public override void Bind(Transform root)
    {
        ScrollView = UINode.From(Find(root, "Scroll View"));
        ScrollView_Viewport = UINode.From(Find(root, "Scroll View/Viewport"));
        ScrollView_Viewport_Content = UINode.From(Find(root, "Scroll View/Viewport/Content"));
        ScrollView_Viewport_Content_Item = UINode.From(Find(root, "Scroll View/Viewport/Content/Item"));
        ScrollView_Viewport_Content_Item_IconBG = UINode.From(Find(root, "Scroll View/Viewport/Content/Item/IconBG"));
        ScrollView_Viewport_Content_Item_IconBG_Mask = UINode.From(Find(root, "Scroll View/Viewport/Content/Item/IconBG/Mask"));
        ScrollView_Viewport_Content_Item_IconBG_Mask_Icon = UINode.From(Find(root, "Scroll View/Viewport/Content/Item/IconBG/Mask/Icon"));
        ScrollView_Viewport_Content_Item_Name = UINode.From(Find(root, "Scroll View/Viewport/Content/Item/Name"));
    }
}
