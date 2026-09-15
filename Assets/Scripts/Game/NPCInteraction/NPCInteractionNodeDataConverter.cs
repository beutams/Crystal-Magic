using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CrystalMagic.Game.Data
{
    public sealed class NPCInteractionNodeDataConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(NPCInteractionNodeData).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            JObject jObject = JObject.Load(reader);
            string typeName = jObject["Type"]?.Value<string>();
            if (!NPCInteractionNodeDataRegistry.TryGetNodeType(typeName, out _))
            {
                throw new JsonSerializationException($"Unknown NPC interaction node type: {typeName}");
            }

            NPCInteractionNodeData node = NPCInteractionNodeDataFactory.Default.CreateNode(typeName, assignGuid: false);
            using JsonReader objectReader = jObject.CreateReader();
            serializer.Populate(objectReader, node);
            node.Guid ??= Guid.NewGuid().ToString("N");
            return node;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            NPCInteractionNodeData node = (NPCInteractionNodeData)value;
            node.Guid ??= Guid.NewGuid().ToString("N");
            if (!NPCInteractionNodeDataRegistry.TryGetNodeKey(node.GetType(), out string typeName))
            {
                throw new JsonSerializationException($"Unregistered NPC interaction node type: {node.GetType().FullName}");
            }

            JObject jObject = new JObject();
            foreach (FieldInfo field in GetSerializableFields(node.GetType()))
            {
                object fieldValue = field.GetValue(node);
                jObject[field.Name] = fieldValue != null
                    ? JToken.FromObject(fieldValue, serializer)
                    : JValue.CreateNull();
            }

            jObject["Type"] = typeName;
            jObject[nameof(NPCInteractionNodeData.Guid)] = node.Guid;
            jObject.WriteTo(writer);
        }

        private static IEnumerable<FieldInfo> GetSerializableFields(Type type)
        {
            for (Type currentType = type; currentType != null && currentType != typeof(object); currentType = currentType.BaseType)
            {
                FieldInfo[] fields = currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                for (int i = 0; i < fields.Length; i++)
                {
                    yield return fields[i];
                }
            }
        }
    }
}
