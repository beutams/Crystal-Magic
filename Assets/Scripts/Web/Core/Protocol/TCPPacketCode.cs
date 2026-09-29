using System;
using System.IO;

namespace Server
{
    public enum PacketReadResult
    {
        NeedMoreData,
        Success,
        Invalid,
    }

    /// <summary>仅负责 TCP 字节流的长度头与拆包；业务消息编解码由 MessageCodec 负责。</summary>
    public static class TCPPacketCode
    {
        private const int HeaderLength = 4;

        public static byte[] Pack(byte[] message)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));
            if (message.Length < MessageCodec.OpcodeLength || message.Length > MessageCodec.MaxMessageLength)
                throw new ArgumentOutOfRangeException(nameof(message), "Invalid network message length.");

            int frameLength = message.Length;
            byte[] packet = new byte[HeaderLength + frameLength];

            packet[0] = (byte)(frameLength >> 24);
            packet[1] = (byte)(frameLength >> 16);
            packet[2] = (byte)(frameLength >> 8);
            packet[3] = (byte)(frameLength);

            Buffer.BlockCopy(message, 0, packet, HeaderLength, message.Length);
            return packet;
        }
        public static PacketReadResult TryUnPack(
            MemoryStream readStream,
            out byte[] message,
            out string error)
        {
            message = null;
            error = null;

            if (readStream.Length < HeaderLength)
                return PacketReadResult.NeedMoreData;

            byte[] buffer = readStream.GetBuffer();
            int messageLength = (buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | buffer[3];
            if (messageLength < MessageCodec.OpcodeLength || messageLength > MessageCodec.MaxMessageLength)
            {
                error = $"包长不合法:{messageLength}";
                return PacketReadResult.Invalid;
            }

            if (readStream.Length < HeaderLength + messageLength)
            {
                return PacketReadResult.NeedMoreData;
            }
            message = new byte[messageLength];
            Buffer.BlockCopy(buffer, HeaderLength, message, 0, messageLength);

            int remainLegth = (int)readStream.Length - messageLength - HeaderLength;
            if(remainLegth > 0)
            {
                Buffer.BlockCopy(buffer, messageLength + HeaderLength, buffer, 0, remainLegth);
            }
            readStream.SetLength(remainLegth);
            readStream.Position = remainLegth;
            return PacketReadResult.Success;
        }
    }
}
