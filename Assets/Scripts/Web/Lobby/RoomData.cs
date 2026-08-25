using System;
using System.Collections.Generic;

namespace Server
{
    [Serializable]
    public class RoomData
    {
        public long roomId;
        public long ownerId;
        public string roomName;
        public int enterNum;
        public int maxNum;
        public Dictionary<long, string> players = new Dictionary<long, string>();

        public static RoomData CreateRoomData(Room room)
        {
            RoomData roomData = new RoomData();
            roomData.roomId = room.roomId;
            roomData.roomName = room.roomName;
            roomData.enterNum = room.enterNum;
            roomData.maxNum = room.maxNum;
            roomData.ownerId = room.ownerId;
            foreach (var item in room.players)
            {
                roomData.players.Add(item.Key, item.Value.username);
            }
            return roomData;
        }
    }
    [Serializable]
    public class RoomListData
    {
        public List<SimpleRoomData> roomList = new List<SimpleRoomData>();

        public static RoomListData CreateRoomData(Dictionary<long, Room> roomList)
        {
            RoomListData listData = new RoomListData();
            foreach (var room in roomList.Values)
            {
                SimpleRoomData data = new SimpleRoomData();
                data.roomId = room.roomId;
                data.ownerId = room.ownerId;
                data.roomName = room.roomName;
                data.enterNum = room.enterNum;
                data.maxNum = room.maxNum;
                listData.roomList.Add(data);
            }
            return listData;
        }
    }
    [Serializable]
    public class SimpleRoomData
    {
        public long roomId;
        public long ownerId;
        public string roomName;
        public int enterNum;
        public int maxNum;
    }
}