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
        public const string Parallel = "Parallel";
        public const string Inverter = "Inverter";
        public const string Succeeder = "Succeeder";
        public const string Failer = "Failer";
        public const string Repeater = "Repeater";
        public const string UntilSuccess = "UntilSuccess";
        public const string UntilFailure = "UntilFailure";
        public const string Cooldown = "Cooldown";
        public const string Timeout = "Timeout";
        public const string CheckCondition = "CheckCondition";
        public const string MoveToTarget = "MoveToTarget";
        public const string CastToTarget = "CastToTarget";
        public const string Wander = "Wander";
        public const string Idle = "Idle";
    }

    public enum ParallelSuccessPolicy
    {
        RequireAll = 0,
        RequireAny = 1,
    }

    public enum ParallelFailurePolicy
    {
        RequireAny = 0,
        RequireAll = 1,
    }

    public enum RepeaterExecutionMode
    {
        ImmediatePerTick = 0,
        OncePerTick = 1,
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
    [FactoryKey(BehaviorNodeTypes.Parallel, 2, "Parallel")]
    public sealed class ParallelBehaviorNodeData : BehaviorNodeData
    {
        public ParallelSuccessPolicy SuccessPolicy = ParallelSuccessPolicy.RequireAll;
        public ParallelFailurePolicy FailurePolicy = ParallelFailurePolicy.RequireAny;

        public ParallelBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.Parallel;
        }
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.Inverter, 20, "Inverter")]
    public sealed class InverterBehaviorNodeData : BehaviorNodeData
    {
        public InverterBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.Inverter;
        }
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.Succeeder, 21, "Succeeder")]
    public sealed class SucceederBehaviorNodeData : BehaviorNodeData
    {
        public SucceederBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.Succeeder;
        }
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.Failer, 22, "Failer")]
    public sealed class FailerBehaviorNodeData : BehaviorNodeData
    {
        public FailerBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.Failer;
        }
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.Repeater, 23, "Repeater")]
    public sealed class RepeaterBehaviorNodeData : BehaviorNodeData
    {
        public RepeaterExecutionMode ExecutionMode = RepeaterExecutionMode.ImmediatePerTick;
        public int RepeatCount = -1;

        public RepeaterBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.Repeater;
        }
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.UntilSuccess, 24, "Until Success")]
    public sealed class UntilSuccessBehaviorNodeData : BehaviorNodeData
    {
        public UntilSuccessBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.UntilSuccess;
        }
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.UntilFailure, 25, "Until Failure")]
    public sealed class UntilFailureBehaviorNodeData : BehaviorNodeData
    {
        public UntilFailureBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.UntilFailure;
        }
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.Cooldown, 26, "Cooldown")]
    public sealed class CooldownBehaviorNodeData : BehaviorNodeData
    {
        public float CooldownSeconds = 1f;

        public CooldownBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.Cooldown;
        }
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.Timeout, 27, "Timeout")]
    public sealed class TimeoutBehaviorNodeData : BehaviorNodeData
    {
        public float TimeoutSeconds = 1f;

        public TimeoutBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.Timeout;
        }
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.CheckCondition, 10, "Check Condition")]
    public sealed class CheckConditionBehaviorNodeData : BehaviorNodeData
    {
        public List<ConditionConfig> Conditions = new();

        public CheckConditionBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.CheckCondition;
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
        public UnitSkillSelectionMode SelectionMode = UnitSkillSelectionMode.RandomAll;
        public int SkillId;
        public int SkillTagMask;

        public CastToTargetBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.CastToTarget;
        }
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.Wander, 15, "Wander")]
    public sealed class WanderBehaviorNodeData : BehaviorNodeData
    {
        public float MinDurationSeconds = 1f;
        public float MaxDurationSeconds = 2f;

        public WanderBehaviorNodeData()
        {
            Type = BehaviorNodeTypes.Wander;
        }
    }

    [Serializable]
    [FactoryKey(BehaviorNodeTypes.Idle, 16, "Idle")]
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
                ParallelBehaviorNodeData parallel => $"{GetDisplayName(parallel.Type)} | {parallel.SuccessPolicy} / {parallel.FailurePolicy}",
                RepeaterBehaviorNodeData repeater => $"{GetDisplayName(repeater.Type)} | {repeater.ExecutionMode} | Count {repeater.RepeatCount}",
                CooldownBehaviorNodeData cooldown => $"{GetDisplayName(cooldown.Type)} | {cooldown.CooldownSeconds:0.##}s",
                TimeoutBehaviorNodeData timeout => $"{GetDisplayName(timeout.Type)} | {timeout.TimeoutSeconds:0.##}s",
                CheckConditionBehaviorNodeData condition => $"{GetDisplayName(condition.Type)} | Conditions {condition.Conditions?.Count ?? 0}",
                MoveToTargetBehaviorNodeData move => $"{GetDisplayName(move.Type)} | Stop {move.StopDistance:0.##}",
                CastToTargetBehaviorNodeData cast => $"{GetDisplayName(cast.Type)} | {cast.SelectionMode}",
                WanderBehaviorNodeData wander => $"{GetDisplayName(wander.Type)} | {wander.MinDurationSeconds:0.##}-{wander.MaxDurationSeconds:0.##}s",
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
                jObject[field.Name] = SerializeFieldValue(fieldValue, serializer);
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

        private static JToken SerializeFieldValue(object fieldValue, JsonSerializer serializer)
        {
            if (fieldValue == null)
                return JValue.CreateNull();

            return fieldValue switch
            {
                Vector2 vector2 => new JObject
                {
                    ["x"] = vector2.x,
                    ["y"] = vector2.y,
                },
                Vector3 vector3 => new JObject
                {
                    ["x"] = vector3.x,
                    ["y"] = vector3.y,
                    ["z"] = vector3.z,
                },
                _ => JToken.FromObject(fieldValue, serializer),
            };
        }
    }
}
