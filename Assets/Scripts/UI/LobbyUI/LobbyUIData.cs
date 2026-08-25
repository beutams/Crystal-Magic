// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class LobbyUIData : UIData
{
    public UINode Background;
    public UINode ScrollView;
    public UINode ScrollView_Viewport;
    public UINode ScrollView_Viewport_Content;
    public UINode ScrollView_Viewport_Content_RoomItem;
    public UINode ScrollView_Viewport_Content_RoomItem_Open;
    public UINode ScrollView_Viewport_Content_RoomItem_Open_CreateTime;
    public UINode ScrollView_Viewport_Content_RoomItem_Open_Money;
    public UINode ScrollView_Viewport_Content_RoomItem_Open_Index;
    public UINode ScrollView_Viewport_Content_RoomItem_Open_Delete;
    public UINode Back;
    public UINode Back_Default;
    public UINode Back_Click;

    public override void Bind(Transform root)
    {
        Background = UINode.From(Find(root, "Background"));
        ScrollView = UINode.From(Find(root, "Scroll View"));
        ScrollView_Viewport = UINode.From(Find(root, "Scroll View/Viewport"));
        ScrollView_Viewport_Content = UINode.From(Find(root, "Scroll View/Viewport/Content"));
        ScrollView_Viewport_Content_RoomItem = UINode.From(Find(root, "Scroll View/Viewport/Content/RoomItem"));
        ScrollView_Viewport_Content_RoomItem_Open = UINode.From(Find(root, "Scroll View/Viewport/Content/RoomItem/Open"));
        ScrollView_Viewport_Content_RoomItem_Open_CreateTime = UINode.From(Find(root, "Scroll View/Viewport/Content/RoomItem/Open/CreateTime"));
        ScrollView_Viewport_Content_RoomItem_Open_Money = UINode.From(Find(root, "Scroll View/Viewport/Content/RoomItem/Open/Money"));
        ScrollView_Viewport_Content_RoomItem_Open_Index = UINode.From(Find(root, "Scroll View/Viewport/Content/RoomItem/Open/Index"));
        ScrollView_Viewport_Content_RoomItem_Open_Delete = UINode.From(Find(root, "Scroll View/Viewport/Content/RoomItem/Open/Delete"));
        Back = UINode.From(Find(root, "Back"));
        Back_Default = UINode.From(Find(root, "Back/Default"));
        Back_Click = UINode.From(Find(root, "Back/Click"));
    }
}
