using UnityEngine;
using CrystalMagic.Core;

public sealed class LobbyRoomUI_PlayerData : UIData
{
    public UINode PlayerName;
    public UINode Ready;
    public UINode Ready_Ready;
    public UINode Ready_Not;
    public UINode KickOut;

    public override void Bind(Transform root)
    {
        PlayerName = UINode.From(Find(root, "PlayerName"));
        Ready = UINode.From(Find(root, "Ready"));
        Ready_Ready = UINode.From(Find(root, "Ready/Ready"));
        Ready_Not = UINode.From(Find(root, "Ready/Not"));
        KickOut = UINode.From(Find(root, "KickOut"));
    }
}
