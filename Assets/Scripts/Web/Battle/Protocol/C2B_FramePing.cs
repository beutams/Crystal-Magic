using System;

namespace Server
{
    [Message(Opcode = 51)]
    [Serializable]
    public class C2B_FramePing : IMessage
    {
        public long clientSendTime;
        public uint sceneVersion;
    }
}
