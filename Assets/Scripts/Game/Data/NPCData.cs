using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using Newtonsoft.Json;

namespace CrystalMagic.Game.Data
{
    [Serializable]
    [ReadOnlyData]
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

    [Serializable]
    [FactoryKey("Dialogue", 0, "Dialogue")]
    public sealed class NPCDialogueInteractionNodeData : NPCInteractionNodeData
    {
        public string Speaker;

        public string ContentKey;
    }

    [Serializable]
    [FactoryKey("OpenUI", 2, "Open UI")]
    public sealed class NPCOpenUIInteractionNodeData : NPCInteractionNodeData
    {
        public string UIName;

        public string OpenData;

        public bool WaitUntilClosed = true;
    }

    [Serializable]
    [FactoryKey("Move", 3, "Move")]
    public sealed class NPCMoveInteractionNodeData : NPCInteractionNodeData
    {
        public string TargetMarker;

        public float StopDistance = 0.5f;

        public bool WaitUntilArrived = true;
    }

    [Serializable]
    [FactoryKey("EnterDungeon", 4, "Enter Dungeon")]
    public sealed class NPCEnterDungeonInteractionNodeData : NPCInteractionNodeData
    {
        public int DungeonFloor = 1;
    }

    [Serializable]
    [FactoryKey("EnterTrainingGround", 5, "Enter Training Ground")]
    public sealed class NPCEnterTrainingGroundInteractionNodeData : NPCInteractionNodeData
    {
    }

    [Serializable]
    [FactoryKey("Select", 1, "Select")]
    public sealed class NPCSelectInteractionNodeData : NPCInteractionNodeData
    {
        public List<NPCSelectOptionData> Options = new();
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
}
