using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using UnityEngine;

public static class NPCInteractionRuntimeUtility
{
    private const string TrainingNpcName = "NPCTraining";
    private const string TrainingInteractionKey = "Training";

    private const string EntrySelectNodeGuid = "training_select";
    private const string DungeonFloorSelectNodeGuid = "training_dungeon_floor_select";
    private const string TrainingGroundNodeGuid = "training_ground";
    private const string DungeonNodeGuidPrefix = "training_dungeon_floor_";

    public static NPCInteractionData ResolveRuntimeInteraction(NPCData npcData, NPCInteractionData interaction)
    {
        if (npcData == null || interaction == null)
            return interaction;

        if (!string.Equals(npcData.NPC, TrainingNpcName, StringComparison.Ordinal) ||
            !string.Equals(interaction.Key, TrainingInteractionKey, StringComparison.Ordinal))
        {
            return interaction;
        }

        return BuildTrainingTeleportInteraction(interaction);
    }

    private static NPCInteractionData BuildTrainingTeleportInteraction(NPCInteractionData sourceInteraction)
    {
        SaveDataComponent saveData = SaveDataComponent.Instance;
        saveData?.EnsureDungeonStartFloorUnlocksInitialized();

        int highestReachedFloor = Mathf.Max(
            1,
            (int)Math.Round(saveData?.GetVariable(SaveDataComponent.DungeonHighestReachedFloorVariableKey, 1d) ?? 1d));
        int maxCandidateFloor = Mathf.Max(
            1,
            ((highestReachedFloor / SaveDataComponent.DungeonStartFloorUnlockInterval) + 1) *
            SaveDataComponent.DungeonStartFloorUnlockInterval + 1);

        NPCSelectInteractionNodeData entrySelectNode = new()
        {
            Guid = EntrySelectNodeGuid,
            Options = new List<NPCSelectOptionData>
            {
                new()
                {
                    DisplayName = "进入地牢",
                    NextNodeGuid = DungeonFloorSelectNodeGuid,
                },
                new()
                {
                    DisplayName = "进入训练营",
                    NextNodeGuid = TrainingGroundNodeGuid,
                },
            },
        };

        NPCSelectInteractionNodeData floorSelectNode = new()
        {
            Guid = DungeonFloorSelectNodeGuid,
            Options = new List<NPCSelectOptionData>(),
        };

        List<NPCInteractionNodeData> nodes = new()
        {
            entrySelectNode,
            floorSelectNode,
            new NPCEnterTrainingGroundInteractionNodeData
            {
                Guid = TrainingGroundNodeGuid,
            },
        };

        for (int floor = 1; floor <= maxCandidateFloor; floor += SaveDataComponent.DungeonStartFloorUnlockInterval)
        {
            string nodeGuid = $"{DungeonNodeGuidPrefix}{floor}";
            floorSelectNode.Options.Add(new NPCSelectOptionData
            {
                DisplayName = $"第{floor}层",
                EnableExpression = floor == 1
                    ? string.Empty
                    : $"{SaveDataComponent.GetDungeonStartFloorUnlockVariableKey(floor)} > 0",
                NextNodeGuid = nodeGuid,
            });

            nodes.Add(new NPCEnterDungeonInteractionNodeData
            {
                Guid = nodeGuid,
                DungeonFloor = floor,
            });
        }

        return new NPCInteractionData
        {
            Key = sourceInteraction.Key,
            DisplayName = sourceInteraction.DisplayName,
            EnableExpression = sourceInteraction.EnableExpression,
            EntryNodeGuid = EntrySelectNodeGuid,
            Nodes = nodes,
        };
    }
}
