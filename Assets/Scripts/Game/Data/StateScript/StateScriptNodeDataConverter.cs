using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CrystalMagic.Game.Data
{
    public sealed class StateScriptNodeDataConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(StateScriptNodeData).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            JObject json = JObject.Load(reader);
            string typeName = json[nameof(StateScriptNodeData.Type)]?.Value<string>();
            if (!StateScriptNodeDataRegistry.TryGetNodeType(typeName, out _))
                throw new JsonSerializationException($"Unknown StateScript node type: {typeName}");

            StateScriptNodeData node = StateScriptNodeDataRegistry.Create(typeName, assignGuid: false);
            using JsonReader objectReader = json.CreateReader();
            serializer.Populate(objectReader, node);
            node.Type = StateScriptNodeDataRegistry.ResolveTypeName(node);
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

            StateScriptNodeData node = (StateScriptNodeData)value;
            node.Type = StateScriptNodeDataRegistry.ResolveTypeName(node);
            node.Guid ??= Guid.NewGuid().ToString("N");

            JObject json = new();
            foreach (FieldInfo field in GetSerializableFields(node.GetType()))
                json[field.Name] = SerializeField(field.GetValue(node), serializer);

            json[nameof(StateScriptNodeData.Type)] = node.Type;
            json[nameof(StateScriptNodeData.Guid)] = node.Guid;
            json.WriteTo(writer);
        }

        private static IEnumerable<FieldInfo> GetSerializableFields(Type type)
        {
            for (Type current = type; current != null && current != typeof(object); current = current.BaseType)
            {
                FieldInfo[] fields = current.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                for (int i = 0; i < fields.Length; i++)
                    yield return fields[i];
            }
        }

        private static JToken SerializeField(object value, JsonSerializer serializer)
        {
            if (value == null)
                return JValue.CreateNull();

            if (value is Vector2 vector2)
                return new JObject { ["x"] = vector2.x, ["y"] = vector2.y };

            return JToken.FromObject(value, serializer);
        }
    }

    // Unity.Mathematics exposes swizzle properties such as xxxx; serialize only stored values.
    public sealed class StateScriptVector2Converter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(Vector2);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return Vector2.zero;

            JObject json = JObject.Load(reader);
            return new Vector2(
                json.Value<float?>("x") ?? 0f,
                json.Value<float?>("y") ?? 0f);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Vector2 vector = (Vector2)value;
            JObject json = new()
            {
                ["x"] = vector.x,
                ["y"] = vector.y,
            };
            json.WriteTo(writer);
        }
    }

    public sealed class StateScriptUnitValueConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(UnitValue);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return UnitValue.None;

            JObject json = JObject.Load(reader);
            UnitValue value = new()
            {
                Type = json[nameof(UnitValue.Type)]?.ToObject<UnitValueType>(serializer) ?? UnitValueType.None,
                Bool = json.Value<bool?>(nameof(UnitValue.Bool)) ?? false,
                Int = json.Value<int?>(nameof(UnitValue.Int)) ?? 0,
                Float = json.Value<float?>(nameof(UnitValue.Float)) ?? 0f,
                String = json.Value<string>(nameof(UnitValue.String)),
            };

            JObject float2 = json[nameof(UnitValue.Float2)] as JObject;
            if (float2 != null)
            {
                value.Float2 = new float2(
                    float2.Value<float?>("x") ?? 0f,
                    float2.Value<float?>("y") ?? 0f);
            }

            JObject float3 = json[nameof(UnitValue.Float3)] as JObject;
            if (float3 != null)
            {
                value.Float3 = new float3(
                    float3.Value<float?>("x") ?? 0f,
                    float3.Value<float?>("y") ?? 0f,
                    float3.Value<float?>("z") ?? 0f);
            }

            JObject entity = json[nameof(UnitValue.Entity)] as JObject;
            if (entity != null)
            {
                value.Entity = new Entity
                {
                    Index = entity.Value<int?>(nameof(Entity.Index)) ?? 0,
                    Version = entity.Value<int?>(nameof(Entity.Version)) ?? 0,
                };
            }

            return value;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            UnitValue unitValue = (UnitValue)value;
            JObject json = new()
            {
                [nameof(UnitValue.Type)] = JToken.FromObject(unitValue.Type, serializer),
                [nameof(UnitValue.Bool)] = unitValue.Bool,
                [nameof(UnitValue.Int)] = unitValue.Int,
                [nameof(UnitValue.Float)] = unitValue.Float,
                [nameof(UnitValue.Float2)] = new JObject
                {
                    ["x"] = unitValue.Float2.x,
                    ["y"] = unitValue.Float2.y,
                },
                [nameof(UnitValue.Float3)] = new JObject
                {
                    ["x"] = unitValue.Float3.x,
                    ["y"] = unitValue.Float3.y,
                    ["z"] = unitValue.Float3.z,
                },
                [nameof(UnitValue.Entity)] = new JObject
                {
                    [nameof(Entity.Index)] = unitValue.Entity.Index,
                    [nameof(Entity.Version)] = unitValue.Entity.Version,
                },
                [nameof(UnitValue.String)] = unitValue.String ?? string.Empty,
            };
            json.WriteTo(writer);
        }
    }
}
