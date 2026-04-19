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

        public IEnumerable<NPCInteractionData> GetEnabledInteractions()
        {
            for (int i = 0; i < Interactions.Count; i++)
            {
                NPCInteractionData interaction = Interactions[i];
                if (interaction != null && interaction.IsEnabled())
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

        public string EntryNodeGuid;

        public List<NPCInteractionNodeData> Nodes = new();

        public bool IsEnabled()
        {
            if (string.IsNullOrWhiteSpace(EnableExpression))
            {
                return true;
            }

            return SaveDataComponent.Instance != null && SaveDataComponent.Instance.Check(EnableExpression);
        }

        public NPCInteractionNodeData GetEntryNode()
        {
            return GetNode(EntryNodeGuid);
        }

        public NPCInteractionNodeData GetNode(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid) || Nodes == null)
            {
                return null;
            }

            for (int i = 0; i < Nodes.Count; i++)
            {
                NPCInteractionNodeData node = Nodes[i];
                if (node != null && string.Equals(node.Guid, guid, StringComparison.Ordinal))
                {
                    return node;
                }
            }

            return null;
        }

        public int GetNodeIndex(string guid)
        {
            if (string.IsNullOrWhiteSpace(guid) || Nodes == null)
            {
                return -1;
            }

            for (int i = 0; i < Nodes.Count; i++)
            {
                NPCInteractionNodeData node = Nodes[i];
                if (node != null && string.Equals(node.Guid, guid, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }

    [Serializable]
    [JsonConverter(typeof(NPCInteractionNodeDataConverter))]
    public abstract class NPCInteractionNodeData
    {
        public string Type;

        public string Guid;

        public List<NPCInteractionBranchData> Branches = new();
    }

    [Serializable]
    public sealed class NPCInteractionBranchData
    {
        public string CheckExpression;

        public string NextNodeGuid;

        public bool IsEnabled()
        {
            if (string.IsNullOrWhiteSpace(CheckExpression))
            {
                return true;
            }

            return SaveDataComponent.Instance != null && SaveDataComponent.Instance.Check(CheckExpression);
        }
    }

    public static class NPCInteractionNodeTypes
    {
        public const string Dialogue = "Dialogue";
        public const string Select = "Select";
        public const string OpenUI = "OpenUI";
        public const string Move = "Move";
        public const string EnterDungeon = "EnterDungeon";
        public const string EnterTrainingGround = "EnterTrainingGround";
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

    [Serializable]
    public sealed class NPCEnterDungeonInteractionNodeData : NPCInteractionNodeData
    {
        public int DungeonFloor = 1;

        public NPCEnterDungeonInteractionNodeData()
        {
            Type = NPCInteractionNodeTypes.EnterDungeon;
        }
    }

    [Serializable]
    public sealed class NPCEnterTrainingGroundInteractionNodeData : NPCInteractionNodeData
    {
        public NPCEnterTrainingGroundInteractionNodeData()
        {
            Type = NPCInteractionNodeTypes.EnterTrainingGround;
        }
    }

    [Serializable]
    public sealed class NPCSelectInteractionNodeData : NPCInteractionNodeData
    {
        public List<NPCSelectOptionData> Options = new();

        public NPCSelectInteractionNodeData()
        {
            Type = NPCInteractionNodeTypes.Select;
        }
    }

    [Serializable]
    public sealed class NPCSelectOptionData
    {
        public string DisplayName;

        public string EnableExpression;

        public string NextNodeGuid;

        public bool IsEnabled()
        {
            if (string.IsNullOrWhiteSpace(EnableExpression))
            {
                return true;
            }

            return SaveDataComponent.Instance != null && SaveDataComponent.Instance.Check(EnableExpression);
        }
    }

    public static class NPCInteractionNodeDataRegistry
    {
        private static readonly string[] s_typeOrder =
        {
            NPCInteractionNodeTypes.Dialogue,
            NPCInteractionNodeTypes.Select,
            NPCInteractionNodeTypes.OpenUI,
            NPCInteractionNodeTypes.Move,
            NPCInteractionNodeTypes.EnterDungeon,
            NPCInteractionNodeTypes.EnterTrainingGround,
        };

        private static readonly Dictionary<string, Type> s_typeMap = new(StringComparer.Ordinal)
        {
            { NPCInteractionNodeTypes.Dialogue, typeof(NPCDialogueInteractionNodeData) },
            { NPCInteractionNodeTypes.Select, typeof(NPCSelectInteractionNodeData) },
            { NPCInteractionNodeTypes.OpenUI, typeof(NPCOpenUIInteractionNodeData) },
            { NPCInteractionNodeTypes.Move, typeof(NPCMoveInteractionNodeData) },
            { NPCInteractionNodeTypes.EnterDungeon, typeof(NPCEnterDungeonInteractionNodeData) },
            { NPCInteractionNodeTypes.EnterTrainingGround, typeof(NPCEnterTrainingGroundInteractionNodeData) },
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
                NPCInteractionNodeTypes.Select => "Select",
                NPCInteractionNodeTypes.OpenUI => "Open UI",
                NPCInteractionNodeTypes.Move => "Move",
                NPCInteractionNodeTypes.EnterDungeon => "Enter Dungeon",
                NPCInteractionNodeTypes.EnterTrainingGround => "Enter Training Ground",
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
                NPCSelectInteractionNodeData select => $"{GetDisplayName(select.Type)} | {select.Options?.Count ?? 0} option(s)",
                NPCOpenUIInteractionNodeData openUI => $"{GetDisplayName(openUI.Type)} | {(string.IsNullOrWhiteSpace(openUI.UIName) ? "Empty" : openUI.UIName)}",
                NPCMoveInteractionNodeData move => $"{GetDisplayName(move.Type)} | {(string.IsNullOrWhiteSpace(move.TargetMarker) ? "Empty" : move.TargetMarker)}",
                NPCEnterDungeonInteractionNodeData enterDungeon => $"{GetDisplayName(enterDungeon.Type)} | Floor {Math.Max(1, enterDungeon.DungeonFloor)}",
                NPCEnterTrainingGroundInteractionNodeData enterTrainingGround => GetDisplayName(enterTrainingGround.Type),
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
