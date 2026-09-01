using Server;
using System.Collections.Generic;

public class Room
{
    public ulong roomId;
    public ulong ownerAccountId;
    public string roomName;
    public int enterNum;
    public int maxNum;
    public Dictionary<ulong, Player> players = new Dictionary<ulong, Player>();

    public bool start;

}
