using System;

namespace Server
{
    [Message(Opcode = 50)]
    [Serializable]
    public class General_FrameStateData : IMessage
    {
        public NetworkFrameData data;

        // Client -> server: monotonically increasing sequence of this client's battle frame.
        public uint clientFrameSequence;

        // Server -> client: latest clientFrameSequence received from that connection.
        public uint acknowledgedClientFrameSequence;
    }
}
