// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class StashUIData : UIData
{
    public UINode InventoryView;
    public UINode InventoryView_Viewport;
    public UINode InventoryView_Viewport_Content;
    public UINode InventoryView_Viewport_Content_InventoryItem;
    public UINode InventoryView_Viewport_Content_InventoryItem_Icon;
    public UINode InventoryView_Viewport_Content_InventoryItem_Count;
    public UINode InventoryView_Viewport_Content_InventoryItem_Name;
    public UINode StashView;
    public UINode StashView_Viewport;
    public UINode StashView_Viewport_Content;
    public UINode StashView_Viewport_Content_StashItem;
    public UINode StashView_Viewport_Content_StashItem_Icon;
    public UINode StashView_Viewport_Content_StashItem_Count;
    public UINode StashView_Viewport_Content_StashItem_Name;

    public override void Bind(Transform root)
    {
        InventoryView = UINode.From(Find(root, "InventoryView"));
        InventoryView_Viewport = UINode.From(Find(root, "InventoryView/Viewport"));
        InventoryView_Viewport_Content = UINode.From(Find(root, "InventoryView/Viewport/Content"));
        InventoryView_Viewport_Content_InventoryItem = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem"));
        InventoryView_Viewport_Content_InventoryItem_Icon = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem/Icon"));
        InventoryView_Viewport_Content_InventoryItem_Count = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem/Count"));
        InventoryView_Viewport_Content_InventoryItem_Name = UINode.From(Find(root, "InventoryView/Viewport/Content/InventoryItem/Name"));
        StashView = UINode.From(Find(root, "StashView"));
        StashView_Viewport = UINode.From(Find(root, "StashView/Viewport"));
        StashView_Viewport_Content = UINode.From(Find(root, "StashView/Viewport/Content"));
        StashView_Viewport_Content_StashItem = UINode.From(Find(root, "StashView/Viewport/Content/StashItem"));
        StashView_Viewport_Content_StashItem_Icon = UINode.From(Find(root, "StashView/Viewport/Content/StashItem/Icon"));
        StashView_Viewport_Content_StashItem_Count = UINode.From(Find(root, "StashView/Viewport/Content/StashItem/Count"));
        StashView_Viewport_Content_StashItem_Name = UINode.From(Find(root, "StashView/Viewport/Content/StashItem/Name"));
    }
}
