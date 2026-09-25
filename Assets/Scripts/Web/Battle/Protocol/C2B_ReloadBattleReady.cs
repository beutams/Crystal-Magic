using System;

namespace Server
{
    [Message(Opcode = 46)]
    [Serializable]
    public class C2B_ReloadBattleReady : IMessage
    {
        public ulong battleId;
        public uint connectVersion;
        public uint sceneVersion;
        public uint snapshotFrame;
    }
}
