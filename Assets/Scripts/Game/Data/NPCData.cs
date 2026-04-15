using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Reflection;

namespace CrystalMagic.Game.Data
{
    [Serializable]
    public class NPCData : DataRow
    {
        public string NPC;

        public string DisplayName;

        public List<NPCInteractionData> Interactions = new();

        public IEnumerable<NPCInteractionData> GetEnabledInteractions(SaveVariableData variables)
        {
            for (int i = 0; i < Interactions.Count; i++)
            {
                NPCInteractionData interaction = Interactions[i];
                if (interaction != null && interaction.IsEnabled(variables))
                {
                    yield return interaction;
                }
            }
        }
    }

    [Serializable]
    public class NPCInteractionData
    {
        public string Key;

        public string DisplayName;

        public string EnableExpression;

        public List<NPCInteractionNodeData> Nodes = new();

        // Legacy field kept for one-time migration into Nodes.
        public string ContentKey;

        public bool IsEnabled(SaveVariableData variables)
        {
            if (string.IsNullOrWhiteSpace(EnableExpression))
            {
                return true;
            }

            return variables != null && variables.Check(EnableExpression);
        }
    }

    [Serializable]
    [JsonConverter(typeof(NPCInteractionNodeDataConverter))]
    public abstract class NPCInteractionNodeData
    {
        public string Type;

        public string Guid;
    }

    public static class NPCInteractionNodeTypes
    {
        public const string Dialogue = "Dialogue";
        public const string OpenUI = "OpenUI";
        public const string Move = "Move";
    }

    [Serializable]
    public sealed class NPCDialogueInteractionNodeData : NPCInteractionNodeData
    {
        public string Speaker;

        public string ContentKey;

        public NPCDialogueInteractionNodeData()
        {
            Type = NPCInteractionNodeTypes.Dialogue;
        }
    }

    [Serializable]
    public sealed class NPCOpenUIInteractionNodeData : NPCInteractionNodeData
    {
        public string UIName;

        public string OpenData;

        public bool WaitUntilClosed = true;

        public NPCOpenUIInteractionNodeData()
        {
            Type = NPCInteractionNodeTypes.OpenUI;
        }
    }

    [Serializable]
    public sealed class NPCMoveInteractionNodeData : NPCInteractionNodeData
    {
        public string TargetMarker;

        public float StopDistance = 0.5f;

        public bool WaitUntilArrived = true;

        public NPCMoveInteractionNodeData()
        {
            Type = NPCInteractionNodeTypes.Move;
        }
    }

    public static class NPCInteractionNodeDataRegistry
    {
        private static readonly string[] s_typeOrder =
        {
            NPCInteractionNodeTypes.Dialogue,
            NPCInteractionNodeTypes.OpenUI,
            NPCInteractionNodeTypes.Move,
        };

        private static readonly Dictionary<string, Type> s_typeMap = new(StringComparer.Ordinal)
        {
            { NPCInteractionNodeTypes.Dialogue, typeof(NPCDialogueInteractionNodeData) },
            { NPCInteractionNodeTypes.OpenUI, typeof(NPCOpenUIInteractionNodeData) },
            { NPCInteractionNodeTypes.Move, typeof(NPCMoveInteractionNodeData) },
        };

        public static IReadOnlyList<string> TypeOrder => s_typeOrder;

        public static bool TryGetNodeType(string typeName, out Type nodeType)
        {
            return s_typeMap.TryGetValue(typeName ?? string.Empty, out nodeType);
        }

        public static string GetDisplayName(string typeName)
        {
            return typeName switch
            {
                NPCInteractionNodeTypes.Dialogue => "Dialogue",
                NPCInteractionNodeTypes.OpenUI => "Open UI",
                NPCInteractionNodeTypes.Move => "Move",
                _ => typeName ?? "Unknown",
            };
        }

        public static string ResolveTypeName(NPCInteractionNodeData node)
        {
            if (node == null)
            {
                return NPCInteractionNodeTypes.Dialogue;
            }

            if (!string.IsNullOrWhiteSpace(node.Type) && s_typeMap.ContainsKey(node.Type))
            {
                return node.Type;
            }

            foreach ((string typeName, Type nodeType) in s_typeMap)
            {
                if (nodeType == node.GetType())
                {
                    return typeName;
                }
            }

            return NPCInteractionNodeTypes.Dialogue;
        }

        public static NPCInteractionNodeData Create(string typeName)
        {
            if (!TryGetNodeType(typeName, out Type nodeType))
            {
                nodeType = typeof(NPCDialogueInteractionNodeData);
                typeName = NPCInteractionNodeTypes.Dialogue;
            }

            NPCInteractionNodeData node = Activator.CreateInstance(nodeType) as NPCInteractionNodeData;
            if (node != null)
            {
                node.Type = typeName;
                node.Guid = System.Guid.NewGuid().ToString("N");
            }

            return node;
        }

        public static string GetSummary(NPCInteractionNodeData node)
        {
            return node switch
            {
                NPCDialogueInteractionNodeData dialogue => $"{GetDisplayName(dialogue.Type)} | {(string.IsNullOrWhiteSpace(dialogue.ContentKey) ? "Empty" : dialogue.ContentKey)}",
                NPCOpenUIInteractionNodeData openUI => $"{GetDisplayName(openUI.Type)} | {(string.IsNullOrWhiteSpace(openUI.UIName) ? "Empty" : openUI.UIName)}",
                NPCMoveInteractionNodeData move => $"{GetDisplayName(move.Type)} | {(string.IsNullOrWhiteSpace(move.TargetMarker) ? "Empty" : move.TargetMarker)}",
                _ => GetDisplayName(ResolveTypeName(node)),
            };
        }
    }

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
            string typeName = jObject[nameof(NPCInteractionNodeData.Type)]?.Value<string>();
            if (!NPCInteractionNodeDataRegistry.TryGetNodeType(typeName, out Type nodeType))
            {
                throw new JsonSerializationException($"Unknown NPC interaction node type: {typeName}");
            }

            NPCInteractionNodeData node = Activator.CreateInstance(nodeType) as NPCInteractionNodeData;
            using JsonReader objectReader = jObject.CreateReader();
            serializer.Populate(objectReader, node);
            node.Type = NPCInteractionNodeDataRegistry.ResolveTypeName(node);
            node.Guid ??= System.Guid.NewGuid().ToString("N");
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
            node.Type = NPCInteractionNodeDataRegistry.ResolveTypeName(node);
            node.Guid ??= System.Guid.NewGuid().ToString("N");

            JObject jObject = new JObject();
            foreach (FieldInfo field in GetSerializableFields(node.GetType()))
            {
                object fieldValue = field.GetValue(node);
                jObject[field.Name] = fieldValue != null
                    ? JToken.FromObject(fieldValue, serializer)
                    : JValue.CreateNull();
            }

            jObject[nameof(NPCInteractionNodeData.Type)] = node.Type;
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
