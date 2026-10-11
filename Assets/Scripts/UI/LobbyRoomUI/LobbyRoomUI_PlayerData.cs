// AUTO-GENERATED — DO NOT EDIT MANUALLY
// Right-click Prefab → Assets/Tools/Generate UIData to regenerate

using UnityEngine;
using CrystalMagic.Core;

public class LobbyRoomUI_PlayerData : UIData
{
    public UINode Surface;
    public UINode Selected;
    public UINode Number;
    public UINode HostIcon;
    public UINode Role;
    public UINode PlayerName;
    public UINode Status;

    public override void Bind(Transform root)
    {
        Surface = UINode.From(Find(root, "Surface"));
        Selected = UINode.From(Find(root, "Selected"));
        Number = UINode.From(Find(root, "Number"));
        HostIcon = UINode.From(Find(root, "HostIcon"));
        Role = UINode.From(Find(root, "Role"));
        PlayerName = UINode.From(Find(root, "PlayerName"));
        Status = UINode.From(Find(root, "Status"));
    }
}
