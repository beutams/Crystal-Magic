using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Server
{
    public enum MessageDecodeResult
    {
        Success,
        UnknownOpcode,
        InvalidPayload,
    }

    /// <summary>所有传输共用的消息格式：[2 字节大端 opcode][UTF-8 JSON]，不含 TCP 长度头。</summary>
    public static class MessageCodec
    {
        public const int OpcodeLength = 2;
        public const int MaxMessageLength = 1024 * 1024;

        private static readonly Dictionary<Type, ushort> messages = new();
        private static readonly Dictionary<ushort, Type> opcodes = new();
        private static bool initialized;
        private static JsonSerializerSettings settings;

        public static void Init()
        {
            if (initialized)
                return;

            messages.Clear();
            opcodes.Clear();
            foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (!type.IsClass || type.IsAbstract)
                    continue;

                MessageAttribute attribute = type.GetCustomAttribute<MessageAttribute>();
                if (attribute == null)
                    continue;

                messages.Add(type, attribute.Opcode);
                opcodes.Add(attribute.Opcode, type);
                UnityEngine.Debug.Log($"[Network][Protocol] {type.Name} => opcode {attribute.Opcode}");
            }

            settings = new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.Auto,
                MaxDepth = 64,
                SerializationBinder = new ProtocolTypeBinder(messages.Keys),
            };
            initialized = true;
        }

        public static ushort GetOpcode<T>(T message) where T : IMessage => messages[message.GetType()];
        public static ushort GetOpcode<T>() where T : IMessage => messages[typeof(T)];

        public static byte[] ToJson(IMessage message)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message, typeof(IMessage), settings));
        }

        public static byte[] Encode(IMessage message) => Encode(GetOpcode(message), ToJson(message));

        public static byte[] Encode(ushort opcode, byte[] payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));
            if (payload.Length > MaxMessageLength - OpcodeLength)
                throw new ArgumentOutOfRangeException(nameof(payload), "Network message exceeds 1 MB.");

            byte[] message = new byte[OpcodeLength + payload.Length];
            message[0] = (byte)(opcode >> 8);
            message[1] = (byte)opcode;
            Buffer.BlockCopy(payload, 0, message, OpcodeLength, payload.Length);
            return message;
        }

        public static MessageDecodeResult TryDecode(
            byte[] packet,
            out ushort opcode,
            out IMessage message,
            out Exception exception)
        {
            opcode = 0;
            message = null;
            exception = null;
            if (packet == null || packet.Length < OpcodeLength || packet.Length > MaxMessageLength)
            {
                exception = new ArgumentException("Invalid network message length.");
                return MessageDecodeResult.InvalidPayload;
            }

            opcode = (ushort)((packet[0] << 8) | packet[1]);
            if (!opcodes.TryGetValue(opcode, out Type type))
                return MessageDecodeResult.UnknownOpcode;

            try
            {
                string json = Encoding.UTF8.GetString(packet, OpcodeLength, packet.Length - OpcodeLength);
                message = (IMessage)JsonConvert.DeserializeObject(json, type, settings);
                return message == null || message.GetType() != type ? MessageDecodeResult.InvalidPayload : MessageDecodeResult.Success;
            }
            catch (Exception decodeException)
            {
                exception = decodeException;
                return MessageDecodeResult.InvalidPayload;
            }
        }

        // 允许多态帧状态，但绝不让远端 $type 触发任意 CLR 类型/程序集的实例化。
        private sealed class ProtocolTypeBinder : ISerializationBinder
        {
            private readonly Dictionary<string, Type> allowed = new();
            private readonly HashSet<Type> visited = new();
            private readonly Type[] projectTypes = Assembly.GetExecutingAssembly().GetTypes();
            private readonly string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            public ProtocolTypeBinder(IEnumerable<Type> roots)
            {
                foreach (Type root in roots) Visit(root);
            }
            private void Visit(Type type)
            {
                if (type == null || !visited.Add(type) || type == typeof(object)) return;
                if (type.IsArray) { Visit(type.GetElementType()); return; }
                if (type.IsGenericType)
                    foreach (Type argument in type.GetGenericArguments()) Visit(argument);
                if (type.Assembly != Assembly.GetExecutingAssembly()) return;
                if (!type.IsAbstract && !type.IsInterface) allowed[type.FullName] = type;
                if (type.IsAbstract || type.IsInterface)
                    foreach (Type candidate in projectTypes)
                        if (!candidate.IsAbstract && !candidate.IsInterface && type.IsAssignableFrom(candidate)) Visit(candidate);
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Instance)) Visit(field.FieldType);
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    if (property.CanRead && property.CanWrite) Visit(property.PropertyType);
            }
            public Type BindToType(string requestedAssembly, string typeName)
            {
                if (requestedAssembly?.Split(',')[0].Trim() != assemblyName || !allowed.TryGetValue(typeName, out Type type))
                    throw new JsonSerializationException("Type is not part of the network protocol: " + typeName);
                return type;
            }
            public void BindToName(Type serializedType, out string requestedAssembly, out string typeName)
            {
                if (!allowed.ContainsKey(serializedType.FullName))
                    throw new JsonSerializationException("Unregistered protocol type: " + serializedType.FullName);
                requestedAssembly = assemblyName;
                typeName = serializedType.FullName;
            }
        }
    }
}
