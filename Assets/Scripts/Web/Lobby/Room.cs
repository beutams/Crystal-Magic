using Server;
using System.Collections.Generic;

public class Room
{
    public long roomId;
    public long ownerId;
    public string roomName;
    public int enterNum;
    public int maxNum;
    public Dictionary<long, Player> players = new Dictionary<long, Player>();

    public bool start;

}