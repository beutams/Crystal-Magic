// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class LobbyRoomUIData : UIData
{
    public UINode Background;
    public UINode RoomName;
    public UINode Content;
    public UINode Content_Player;
    public UINode Content_Player_PlayerName;
    public UINode Content_Player_Ready;
    public UINode Content_Player_Ready_Ready;
    public UINode Content_Player_Ready_Not;
    public UINode Content_Player_KickOut;
    public UINode Confirm;
    public UINode Confirm_Default;
    public UINode Confirm_Default_Text;
    public UINode Confirm_Click;
    public UINode Confirm_Click_Text;
    public UINode Cancel;
    public UINode Cancel_Default;
    public UINode Cancel_Default_Text;
    public UINode Cancel_Click;
    public UINode Cancel_Click_Text;
    public override void Bind(Transform root)
    {
        Background = UINode.From(Find(root, "Background"));
        RoomName = UINode.From(Find(root, "RoomName"));
        Content = UINode.From(Find(root, "Content"));
        Content_Player = UINode.From(Find(root, "Content/Player"));
        Content_Player_PlayerName = UINode.From(Find(root, "Content/Player/PlayerName"));
        Content_Player_Ready = UINode.From(Find(root, "Content/Player/Ready"));
        Content_Player_Ready_Ready = UINode.From(Find(root, "Content/Player/Ready/Ready"));
        Content_Player_Ready_Not = UINode.From(Find(root, "Content/Player/Ready/Not"));
        Content_Player_KickOut = UINode.From(Find(root, "Content/Player/KickOut"));
        Confirm = UINode.From(Find(root, "Confirm"));
        Confirm_Default = UINode.From(Find(root, "Confirm/Default"));
        Confirm_Default_Text = UINode.From(Find(root, "Confirm/Default/Text"));
        Confirm_Click = UINode.From(Find(root, "Confirm/Click"));
        Confirm_Click_Text = UINode.From(Find(root, "Confirm/Click/Text"));
        Cancel = UINode.From(Find(root, "Cancel"));
        Cancel_Default = UINode.From(Find(root, "Cancel/Default"));
        Cancel_Default_Text = UINode.From(Find(root, "Cancel/Default/Text"));
        Cancel_Click = UINode.From(Find(root, "Cancel/Click"));
        Cancel_Click_Text = UINode.From(Find(root, "Cancel/Click/Text"));
    }
}
