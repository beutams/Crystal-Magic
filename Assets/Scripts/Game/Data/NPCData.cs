using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using Newtonsoft.Json;

namespace CrystalMagic.Game.Data
{
    [Serializable]
    public class NPCData : DataRow
    {
        public string NPC;

        public string PrefabPath;

        public string DisplayNameKey;

        public List<NPCInteractionData> Interactions = new();

        [JsonIgnore]
        public string DisplayName => LocalizationComponent.Resolve(DisplayNameKey);

        public IEnumerable<NPCInteractionData> GetEnabledInteractions(bool automatic = false)
        {
            for (int i = 0; i < Interactions.Count; i++)
            {
                NPCInteractionData interaction = Interactions[i];
                if (interaction != null && interaction.Automatic == automatic && interaction.IsEnabled())
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

        public string DisplayNameKey;

        public string EnableExpression;

        public bool Automatic;

        // A scene sequence releases its initiating transaction so other NPCs remain usable.
        public bool IsSequence;

        public int AutoPriority = 100;

        public float RetrySeconds = 2f;

        // An absent completion variable deliberately skips legacy saves.
        public string CompletionVariable;

        public string EntryNodeGuid;

        public List<NPCInteractionNodeData> Nodes = new();

        [JsonIgnore]
        public string DisplayName => LocalizationComponent.Resolve(DisplayNameKey);

        public bool IsEnabled()
        {
            if (!string.IsNullOrWhiteSpace(CompletionVariable) &&
                (SaveDataComponent.Instance == null ||
                 !SaveDataComponent.Instance.ContainsVariable(CompletionVariable) ||
                 SaveDataComponent.Instance.GetVariable(CompletionVariable) >= 1d))
                return false;

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

        public GameWorldExecutionTarget ExecutionTargets =
            GameWorldExecutionTarget.Standalone | GameWorldExecutionTarget.Server;

        public List<NPCInteractionBranchData> Branches = new();
    }

    [Serializable]
    public sealed class NPCInteractionBranchData
    {
        public string CheckExpression;

        public string NextNodeGuid;

        public NPCWaitConditionInteractionNodeData RuntimeCondition;

        public bool IsEnabled()
        {
            if (RuntimeCondition != null && !NPCSequenceUtility.Check(RuntimeCondition)) return false;
            if (string.IsNullOrWhiteSpace(CheckExpression))
            {
                return true;
            }

            return SaveDataComponent.Instance != null && SaveDataComponent.Instance.Check(CheckExpression);
        }
    }

    public enum NPCDialogueAnchor
    {
        Target = 0,
        Actor = 1,
    }

    [Serializable]
    [FactoryKey("Dialogue", 0, "Dialogue")]
    public sealed class NPCDialogueInteractionNodeData : NPCInteractionNodeData
    {
        public string Speaker;

        public string ContentKey;

        public NPCDialogueAnchor SpeakerAnchor;
        public float CharactersPerSecond = 20f;
        public float WorldYOffset = 1.35f;
        public float LingerSeconds = 1.2f;
        public bool LockCamera = true;
        public float CameraFollowSmooth = 8f;

        public NPCDialogueInteractionNodeData()
        {
            ExecutionTargets = GameWorldExecutionTarget.All;
        }
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

    [Serializable, FactoryKey("Camera", 8, "镜头移动")]
    public sealed class NPCCameraInteractionNodeData : NPCInteractionNodeData
    {
        public NPCCameraInteractionNodeData() { ExecutionTargets = GameWorldExecutionTarget.Standalone; }
        public string Target = "actor";
        public float Duration = 1.2f;
        public float Smooth = 5f;
    }

    [Serializable, FactoryKey("Guide", 9, "引导遮罩")]
    public sealed class NPCGuideInteractionNodeData : NPCInteractionNodeData
    {
        public NPCGuideInteractionNodeData() { ExecutionTargets = GameWorldExecutionTarget.Standalone; }
        public string Target;
        public string SecondaryTarget;
        public string ContentKey;
        public string FallbackTarget;
        public string FallbackContentKey;
        public bool Clear;
    }

    public enum NPCWaitCondition { Always, Expression, OwnItem, MagicStone, PropSlot, SkillPrefix, UIClosed, UIOpen, All }

    [Serializable, FactoryKey("WaitCondition", 10, "等待实际条件")]
    public sealed class NPCWaitConditionInteractionNodeData : NPCInteractionNodeData
    {
        public NPCWaitConditionInteractionNodeData() { ExecutionTargets = GameWorldExecutionTarget.Standalone; }
        public NPCWaitCondition Condition;
        public string Value;
        public int ItemId = -1;
        public int Slot;
        public List<int> Items = new();
        public bool AllowInventory;
        public List<NPCWaitConditionInteractionNodeData> Conditions = new();
    }

    [Serializable, FactoryKey("Progress", 11, "保存剧情进度")]
    public sealed class NPCProgressInteractionNodeData : NPCInteractionNodeData
    {
        public NPCProgressInteractionNodeData() { ExecutionTargets = GameWorldExecutionTarget.Standalone; }
        public string Variable;
        public double Value;
    }

    [Serializable]
    [FactoryKey("EnterDungeon", 4, "Enter Dungeon")]
    public sealed class NPCEnterDungeonInteractionNodeData : NPCInteractionNodeData
    {
        public int DungeonThemeId;
    }

    [Serializable]
    [FactoryKey("EnterTrainingGround", 5, "Enter Training Ground")]
    public sealed class NPCEnterTrainingGroundInteractionNodeData : NPCInteractionNodeData
    {
    }

    [Serializable]
    [FactoryKey("EnterTown", 6, "Enter Town")]
    public sealed class NPCEnterTownInteractionNodeData : NPCInteractionNodeData
    {
    }

    [Serializable]
    [FactoryKey("Select", 1, "Select")]
    public sealed class NPCSelectInteractionNodeData : NPCInteractionNodeData
    {
        public string Dialog;

        public List<NPCSelectOptionData> Options = new();
    }

    [Serializable]
    [FactoryKey("RequestBattleExit", 7, "联机出口请求")]
    public sealed class NPCRequestBattleExitInteractionNodeData : NPCInteractionNodeData
    {
        public Server.BattleExitRequestType RequestType;

        public NPCRequestBattleExitInteractionNodeData()
        {
            ExecutionTargets = GameWorldExecutionTarget.Client;
        }
    }

    [Serializable]
    public sealed class NPCSelectOptionData
    {
        public string DisplayNameKey;

        public string EnableExpression;

        public string NextNodeGuid;

        [JsonIgnore]
        public string DisplayName => LocalizationComponent.Resolve(DisplayNameKey);

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
