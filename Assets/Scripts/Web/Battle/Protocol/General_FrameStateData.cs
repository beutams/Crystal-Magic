using System;

namespace Server
{
    [Message(Opcode = 50)]
    [Serializable]
    public class General_FrameStateData : IMessage
    {
        public NetworkFrameData data;
    }
}
