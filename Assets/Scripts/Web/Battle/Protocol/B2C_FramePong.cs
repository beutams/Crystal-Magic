using System;

namespace Server
{
    [Message(Opcode = 52)]
    [Serializable]
    public class B2C_FramePong : IMessage
    {
        public long clientSendTime;
        public uint sceneVersion;
        public bool running;
        // currentFrame 表示服务器下一轮将执行的帧；elapsed 是该帧间隔内经过的时间。
        public uint serverFrame;
        public double frameElapsedMs;
    }
}
