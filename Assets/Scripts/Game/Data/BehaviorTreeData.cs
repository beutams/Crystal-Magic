using System;
using System.Collections.Generic;
using System.Reflection;
using CrystalMagic.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace CrystalMagic.Game.Data
{
    [Serializable]
    [ReadOnlyData]
    public sealed class BehaviorTreeData : DataRow
    {
        public string Name;
        public string Description;
        public string RootNodeGuid;
        public List<BehaviorNodeData> Nodes = new();

        public BehaviorNodeData GetRootNode()
        {
            return GetNode(RootNodeGuid);
        }

        public BehaviorNodeData GetNode(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid) || Nodes == null)
                return null;

            for (int i = 0; i < Nodes.Count; i++)
            {
                BehaviorNodeData node = Nodes[i];
                if (node != null && string.Equals(node.Guid, guid, StringComparison.Ordinal))
                    return node;
            }

            return null;
        }

        public int GetNodeIndex(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid) || Nodes == null)
                return -1;

            for (int i = 0; i < Nodes.Count; i++)
            {
                BehaviorNodeData node = Nodes[i];
                if (node != null && string.Equals(node.Guid, guid, StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }
    }

    [Serializable]
    [JsonConverter(typeof(BehaviorNodeDataConverter))]
    public abstract class BehaviorNodeData
    {
        public string Type;
        public string Guid;
        public Vector2 EditorPosition;
        public List<string> ChildGuids = new();
    }

    public static class BehaviorNodeTypes
    {
        public const string Root = "Root";
        public const string Selector = "Selector";
        public const string Sequence = "Sequence";
        public const string HasTarget = "HasTarget";
        public const string AcquireNearestEnemy = "AcquireNearestEnemy";
        public const string TargetInCastRange = "TargetInCastRange";
        public const string MoveToTarget = "MoveToTarget";
        public const string CastToTarget = "CastToTarget";
        public const string Idle = "Idle";
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.Root, -100, "Root")]
    public sealed class RootBehaviorNodeData : BehaviorNodeData
    {
        public RootBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.Root;
        }
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.Selector, 0, "Selector")]
    public sealed class SelectorBehaviorNodeData : BehaviorNodeData
    {
        public SelectorBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.Selector;
        }
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.Sequence, 1, "Sequence")]
    public sealed class SequenceBehaviorNodeData : BehaviorNodeData
    {
        public SequenceBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.Sequence;
        }
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.HasTarget, 10, "Has Target")]
    public sealed class HasTargetBehaviorNodeData : BehaviorNodeData
    {
        public HasTargetBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.HasTarget;
        }
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.AcquireNearestEnemy, 11, "Acquire Nearest Enemy")]
    public sealed class AcquireNearestEnemyBehaviorNodeData : BehaviorNodeData
    {
        public AcquireNearestEnemyBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.AcquireNearestEnemy;
        }
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.TargetInCastRange, 12, "Target In Cast Range")]
    public sealed class TargetInCastRangeBehaviorNodeData : BehaviorNodeData
    {
        public float RangePadding = 0.1f;

        public TargetInCastRangeBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.TargetInCastRange;
        }
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.MoveToTarget, 13, "Move To Target")]
    public sealed class MoveToTargetBehaviorNodeData : BehaviorNodeData
    {
        public float StopDistance = 0.05f;

        public MoveToTargetBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.MoveToTarget;
        }
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.CastToTarget, 14, "Cast To Target")]
    public sealed class CastToTargetBehaviorNodeData : BehaviorNodeData
    {
        public CastToTargetBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.CastToTarget;
        }
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.Idle, 15, "Idle")]
    public sealed class IdleBehaviorNodeData : BehaviorNodeData
    {
        public IdleBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.Idle;
        }
    }

    public static class BehaviorNodeDataRegistry
    {
        private static readonly BehaviorNodeDataFactory s_factory = CreateFactory();

        public static IReadOnlyList<FactoryTypeInfo> TypeInfos => AutoGeneratedRegistry.BehaviorNodeDataTypeInfos;

        public static IReadOnlyList<string> TypeOrder => AutoGeneratedRegistry.BehaviorNodeDataTypeOrder;

        public static bool TryGetNodeType(string typeName, out Type nodeType)
        {
            return AutoGeneratedRegistry.TryGetBehaviorNodeDataType(typeName, out nodeType);
        }

        public static string GetDisplayName(string typeName)
        {
            return AutoGeneratedRegistry.GetBehaviorNodeDataDisplayName(typeName);
        }

        public static string ResolveTypeName(BehaviorNodeData node)
        {
            if (node == null)
                return DefaultTypeName;

            if (!string.IsNullOrWhiteSpace(node.Type) &&
                AutoGeneratedRegistry.ContainsBehaviorNodeDataKey(node.Type))
            {
                return node.Type;
            }

            if (AutoGeneratedRegistry.TryGetBehaviorNodeDataKey(node.GetType(), out string typeName))
                return typeName;

            return DefaultTypeName;
        }

        public static BehaviorNodeData Create(string typeName)
        {
            if (!AutoGeneratedRegistry.ContainsBehaviorNodeDataKey(typeName))
                typeName = DefaultTypeName;

            return s_factory.CreateNode(typeName);
        }

        public static string GetSummary(BehaviorNodeData node)
        {
            return node switch
            {
                TargetInCastRangeBehaviorNodeData range => $"{GetDisplayName(range.Type)} | Padding {range.RangePadding:0.##}",
                MoveToTargetBehaviorNodeData move => $"{GetDisplayName(move.Type)} | Stop {move.StopDistance:0.##}",
                _ => GetDisplayName(ResolveTypeName(node)),
            };
        }

        private static string DefaultTypeName =>
            string.IsNullOrWhiteSpace(AutoGeneratedRegistry.DefaultBehaviorNodeDataKey)
                ? BehaviorNodeTypes.Idle
                : AutoGeneratedRegistry.DefaultBehaviorNodeDataKey;

        private static BehaviorNodeDataFactory CreateFactory()
        {
            var factory = new BehaviorNodeDataFactory();
            AutoGeneratedRegistry.RegisterBehaviorNodeData(factory);
            return factory;
        }
    }

    public sealed class BehaviorNodeDataConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            return typeof(BehaviorNodeData).IsAssignableFrom(objectType);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            JObject jObject = JObject.Load(reader);
            string typeName = jObject[nameof(BehaviorNodeData.Type)]?.Value<string>();
            if (!BehaviorNodeDataRegistry.TryGetNodeType(typeName, out _))
                throw new JsonSerializationException($"Unknown behavior node type: {typeName}");

            BehaviorNodeData node = BehaviorNodeDataRegistry.Create(typeName);
            using JsonReader objectReader = jObject.CreateReader();
            serializer.Populate(objectReader, node);
            node.Type = BehaviorNodeDataRegistry.ResolveTypeName(node);
            node.Guid ??= System.Guid.NewGuid().ToString("N");
            node.ChildGuids ??= new List<string>();
            return node;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            BehaviorNodeData node = (BehaviorNodeData)value;
            node.Type = BehaviorNodeDataRegistry.ResolveTypeName(node);
            node.Guid ??= System.Guid.NewGuid().ToString("N");
            node.ChildGuids ??= new List<string>();

            JObject jObject = new JObject();
            foreach (FieldInfo field in GetSerializableFields(node.GetType()))
            {
                object fieldValue = field.GetValue(node);
                jObject[field.Name] = fieldValue != null
                    ? JToken.FromObject(fieldValue, serializer)
                    : JValue.CreateNull();
            }

            jObject[nameof(BehaviorNodeData.Type)] = node.Type;
            jObject[nameof(BehaviorNodeData.Guid)] = node.Guid;
            jObject.WriteTo(writer);
        }

        private static IEnumerable<FieldInfo> GetSerializableFields(Type type)
        {
            for (Type currentType = type; currentType != null && currentType != typeof(object); currentType = currentType.BaseType)
            {
                FieldInfo[] fields = currentType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
                for (int i = 0; i < fields.Length; i++)
                    yield return fields[i];
            }
        }
    }
}
