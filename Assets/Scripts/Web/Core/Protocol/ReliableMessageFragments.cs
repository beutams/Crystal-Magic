using System;

namespace Server
{
    // 底层必须可靠且有序；每连接最多保留一条 1 MB 的重组消息。
    public sealed class ReliableMessageFragments
    {
        public const int HeaderLength = 12;
        public const int FragmentSize = 64 * 1024;
        private const int Magic = 0x434D5031; // CMP1
        private byte[] buffer;
        private int received;
        private long startedAt;
        public bool IsExpired(long now) => buffer != null && now - startedAt >= ServerUtility.Timeout;

        public static byte[] Pack(byte[] message, int offset)
        {
            if (message == null || message.Length < MessageCodec.OpcodeLength || message.Length > MessageCodec.MaxMessageLength ||
                offset < 0 || offset >= message.Length)
                throw new ArgumentException("Invalid fragment source.");
            int size = Math.Min(FragmentSize - HeaderLength, message.Length - offset);
            byte[] packet = new byte[HeaderLength + size];
            WriteInt(packet, 0, Magic);
            WriteInt(packet, 4, message.Length);
            WriteInt(packet, 8, offset);
            Buffer.BlockCopy(message, offset, packet, HeaderLength, size);
            return packet;
        }

        public bool TryReceive(byte[] packet, long now, out byte[] message)
        {
            message = null;
            if (packet == null || packet.Length <= HeaderLength || packet.Length > FragmentSize || IsExpired(now) ||
                ReadInt(packet, 0) != Magic) return false;
            int length = ReadInt(packet, 4);
            int offset = ReadInt(packet, 8);
            int size = packet.Length - HeaderLength;
            if (length < MessageCodec.OpcodeLength || length > MessageCodec.MaxMessageLength ||
                offset != received || offset < 0 || offset > length - size) return false;
            if (buffer == null)
            {
                if (offset != 0) return false;
                buffer = new byte[length];
                startedAt = now;
            }
            if (buffer.Length != length) return false;
            Buffer.BlockCopy(packet, HeaderLength, buffer, received, size);
            received += size;
            if (received == length) { message = buffer; Clear(); }
            return true;
        }
        public void Clear() { buffer = null; received = 0; startedAt = 0; }
        private static int ReadInt(byte[] data, int offset) =>
            (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        private static void WriteInt(byte[] data, int offset, int value)
        {
            data[offset] = (byte)(value >> 24); data[offset + 1] = (byte)(value >> 16);
            data[offset + 2] = (byte)(value >> 8); data[offset + 3] = (byte)value;
        }
    }
}
