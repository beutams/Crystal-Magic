using System;
using System.Collections.Generic;

namespace Server
{
    [Serializable]
    public class RoomData
    {
        public ulong roomId;
        public ulong ownerAccountId;
        public string roomName;
        public int enterNum;
        public int maxNum;
        public int themeKey;
        public bool start;
        public Dictionary<ulong, string> players = new Dictionary<ulong, string>();
        public Dictionary<ulong, bool> playerready = new Dictionary<ulong, bool>();
        public static RoomData CreateRoomData(Room room)
        {
            RoomData roomData = new RoomData();
            roomData.roomId = room.roomId;
            roomData.roomName = room.roomName;
            roomData.enterNum = room.enterNum;
            roomData.maxNum = room.maxNum;
            roomData.themeKey = room.themeKey;
            roomData.start = room.start;
            roomData.ownerAccountId = room.ownerAccountId;
            foreach (var item in room.players)
            {
                roomData.players.Add(item.Key, item.Value.username);
                roomData.playerready.Add(item.Key, item.Value.ready);
            }
            return roomData;
        }
    }
    [Serializable]
    public class RoomListData
    {
        public List<SimpleRoomData> roomList = new List<SimpleRoomData>();

        public static RoomListData CreateRoomData(Dictionary<ulong, Room> roomList)
        {
            RoomListData listData = new RoomListData();
            foreach (var room in roomList.Values)
            {
                SimpleRoomData data = new SimpleRoomData();
                data.roomId = room.roomId;
                data.ownerAccountId = room.ownerAccountId;
                data.roomName = room.roomName;
                data.enterNum = room.enterNum;
                data.maxNum = room.maxNum;
                data.themeKey = room.themeKey;
                listData.roomList.Add(data);
            }
            return listData;
        }
    }
    [Serializable]
    public class SimpleRoomData
    {
        public ulong roomId;
        public ulong ownerAccountId;
        public string roomName;
        public int enterNum;
        public int maxNum;
        public int themeKey;
    }
}
