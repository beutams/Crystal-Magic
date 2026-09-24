using System;

namespace Server
{
    [Message(Opcode = 42)]
    [Serializable]
    public class B2C_StartFrame : IMessage
    {
        public ulong battleId;
        public uint connectVersion;
        public uint sceneVersion;
        public uint startFrame;
        public int frameInterval = 33;
        public double frameElapsedMs;
    }
}
