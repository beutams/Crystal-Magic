using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace Server
{
    public static class TCPPacketCode
    {
        private const int HeaderLength = 4;
        private const int OpcodeLength = 2;
        private const int MaxFrameLength = 1024 * 1024; // 1 MB

        public static Dictionary<Type, ushort> messages = new Dictionary<Type, ushort>();
        public static Dictionary<ushort, Type> opcodes = new Dictionary<ushort, Type>();
        private static bool initialized;

        public static void Init()
        {
            if (initialized)
            {
                return;
            }

            messages.Clear();
            opcodes.Clear();
            Assembly assembly = Assembly.GetExecutingAssembly();
            foreach (Type type in assembly.GetTypes())
            {
                if (type.IsClass && !type.IsAbstract)
                {
                    MessageAttribute messageAttribute;
                    if ((messageAttribute = type.GetCustomAttribute<MessageAttribute>()) != null)
                    {
                        ushort opcode = messageAttribute.Opcode;
                        messages.Add(type, opcode);
                        opcodes.Add(opcode, type);
                        UnityEngine.Debug.Log($"[TCP][Protocol] {type.Name} => opcode {opcode}");
                    }
                }
            }

            initialized = true;
        }
        public static ushort GetOpcode<T>(T message) where T : IMessage
        {
            return messages[message.GetType()];
        }
        public static ushort GetOpcode<T>() where T : IMessage
        {
            return messages[typeof(T)];
        }
        public static byte[] ToJson(IMessage message)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                TypeNameHandling = TypeNameHandling.Auto
            };
            string json = JsonConvert.SerializeObject(message, typeof(IMessage), settings);
            return Encoding.UTF8.GetBytes(json);
        }
        public static IMessage ToMessage(byte[] bytes, ushort opcode)
        {
            string json = Encoding.UTF8.GetString(bytes);
            Type type = opcodes[opcode];
            return (IMessage)JsonConvert.DeserializeObject(json,type);
        }
        public static byte[] Pack(ushort opcode, byte[] message)
        {
            int frameLength = OpcodeLength + message.Length;
            byte[] packet = new byte[HeaderLength + frameLength];

            packet[0] = (byte)(frameLength >> 24);
            packet[1] = (byte)(frameLength >> 16);
            packet[2] = (byte)(frameLength >> 8);
            packet[3] = (byte)(frameLength);

            packet[4] = (byte)(opcode >> 8);
            packet[5] = (byte)(opcode);

            Buffer.BlockCopy(message, 0, packet, 6, message.Length);
            return packet;
        }
        public static bool TryUnPack(MemoryStream readStream,out byte[] body,out ushort opcode)
        {
            opcode = 0;
            body = null;

            if (readStream.Length < HeaderLength)
                return false;

            byte[] buffer = readStream.GetBuffer();
            int messageLength = GetMessageLength(buffer);
            if (readStream.Length < HeaderLength + messageLength)
            {
                return false;
            }
            int bodyLength = messageLength - OpcodeLength;
            opcode = (ushort)((buffer[4] << 8) | buffer[5]);

            body = new byte[bodyLength];
            Buffer.BlockCopy(buffer, HeaderLength + OpcodeLength, body, 0, bodyLength);

            int remainLegth = (int)readStream.Length - messageLength - HeaderLength;
            if(remainLegth > 0)
            {
                Buffer.BlockCopy(buffer, messageLength + HeaderLength, buffer, 0, remainLegth);
            }
            readStream.SetLength(remainLegth);
            readStream.Position = remainLegth;
            return true;
        }
        private static int GetMessageLength(byte[] buffer)
        {
            int frameLength = (buffer[0] << 24) | (buffer[1] << 16) | (buffer[2] << 8) | buffer[3];
            if(frameLength < OpcodeLength || frameLength > MaxFrameLength)
            {
                throw new InvalidDataException($"包长不合法:{frameLength}");
            }
            return frameLength;
        }
    }
}
