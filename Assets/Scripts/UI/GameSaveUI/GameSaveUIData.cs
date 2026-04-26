// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class GameSaveUIData : UIData
{
    public UINode ScrollView;
    public UINode ScrollView_Viewport;
    public UINode ScrollView_Viewport_Content;
    public UINode ScrollView_Viewport_Content_SaveItem;
    public UINode ScrollView_Viewport_Content_SaveItem_Open;
    public UINode ScrollView_Viewport_Content_SaveItem_Open_CreateTime;
    public UINode ScrollView_Viewport_Content_SaveItem_Open_Money;
    public UINode ScrollView_Viewport_Content_SaveItem_Open_Index;
    public UINode ScrollView_Viewport_Content_SaveItem_Open_DeleteBtn;
    public UINode ScrollView_Viewport_Content_SaveItem_Close;
    public UINode Back;

    public override void Bind(Transform root)
    {
        ScrollView = UINode.From(Find(root, "Scroll View"));
        ScrollView_Viewport = UINode.From(Find(root, "Scroll View/Viewport"));
        ScrollView_Viewport_Content = UINode.From(Find(root, "Scroll View/Viewport/Content"));
        ScrollView_Viewport_Content_SaveItem = UINode.From(Find(root, "Scroll View/Viewport/Content/SaveItem"));
        ScrollView_Viewport_Content_SaveItem_Open = UINode.From(Find(root, "Scroll View/Viewport/Content/SaveItem/Open"));
        ScrollView_Viewport_Content_SaveItem_Open_CreateTime = UINode.From(Find(root, "Scroll View/Viewport/Content/SaveItem/Open/CreateTime"));
        ScrollView_Viewport_Content_SaveItem_Open_Money = UINode.From(Find(root, "Scroll View/Viewport/Content/SaveItem/Open/Money"));
        ScrollView_Viewport_Content_SaveItem_Open_Index = UINode.From(Find(root, "Scroll View/Viewport/Content/SaveItem/Open/Index"));
        ScrollView_Viewport_Content_SaveItem_Open_DeleteBtn = UINode.From(Find(root, "Scroll View/Viewport/Content/SaveItem/Open/DeleteBtn"));
        ScrollView_Viewport_Content_SaveItem_Close = UINode.From(Find(root, "Scroll View/Viewport/Content/SaveItem/Close"));
        Back = UINode.From(Find(root, "Back"));
    }
}
