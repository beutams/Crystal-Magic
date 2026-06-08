using System;
using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using CrystalMagic.UI;
using Unity.Entities;

internal static class DungeonExitInteractionGraph
{
    private const string SelectNodeId = "select";
    private const string NextFloorNodeId = "next_floor";
    private const string RetreatNodeId = "retreat";

    private static readonly NPCInteractionNodeFactory s_nodeFactory = CreateFactory();
    private static NPCInteractionSession s_activeSession;

    public static bool TryOpen(int targetFloor)
    {
        if (s_activeSession != null && s_activeSession.IsActive)
            return false;

        if (UIComponent.Instance == null)
            return false;

        NPCInteractionData interaction = BuildInteraction(Math.Max(1, targetFloor));
        NPCData npcData = new NPCData
        {
            NPC = "DungeonExit",
            DisplayName = "Dungeon Exit",
            Interactions = new List<NPCInteractionData> { interaction },
        };

        s_activeSession = new NPCInteractionSession(Entity.Null, npcData, interaction);
        AdvanceSessionUntilBlocked(0f);
        return true;
    }

    public static void Tick(float deltaTime)
    {
        if (s_activeSession == null)
            return;

        if (!s_activeSession.IsActive)
        {
            s_activeSession = null;
            return;
        }

        AdvanceSessionUntilBlocked(deltaTime);
    }

    private static NPCInteractionData BuildInteraction(int targetFloor)
    {
        NPCSelectInteractionNodeData selectNode = new NPCSelectInteractionNodeData
        {
            Guid = SelectNodeId,
            Options = new List<NPCSelectOptionData>
            {
                new()
                {
                    DisplayName = "\u7ee7\u7eed\u4e0b\u4e00\u5c42",
                    NextNodeGuid = NextFloorNodeId,
                },
                new()
                {
                    DisplayName = "\u64a4\u79bb\u5730\u7262",
                    NextNodeGuid = RetreatNodeId,
                },
            },
        };

        NPCEnterDungeonInteractionNodeData nextFloorNode = new NPCEnterDungeonInteractionNodeData
        {
            Guid = NextFloorNodeId,
            DungeonFloor = targetFloor,
        };

        NPCEnterTownInteractionNodeData retreatNode = new NPCEnterTownInteractionNodeData
        {
            Guid = RetreatNodeId,
        };

        return new NPCInteractionData
        {
            Key = "DungeonExit",
            DisplayName = "Dungeon Exit",
            EntryNodeGuid = SelectNodeId,
            Nodes = new List<NPCInteractionNodeData>
            {
                selectNode,
                nextFloorNode,
                retreatNode,
            },
        };
    }

    private static void AdvanceSessionUntilBlocked(float deltaTime)
    {
        if (s_activeSession == null || !s_activeSession.IsActive)
            return;

        int maxSteps = s_activeSession.Interaction?.Nodes?.Count + 1 ?? 1;
        for (int i = 0; i < maxSteps; i++)
        {
            NPCInteractionNodeData currentNode = s_activeSession.GetCurrentNode();
            if (currentNode == null)
            {
                FinishSession(wasCancelled: false);
                return;
            }

            if (s_activeSession.CurrentRunner == null)
            {
                s_activeSession.CurrentRunner = s_nodeFactory.Create(currentNode);
                if (s_activeSession.CurrentRunner == null)
                {
                    s_activeSession.CurrentNodeGuid = ResolveNextNodeGuid(s_activeSession, currentNode, null);
                    continue;
                }

                s_activeSession.SelectedNextNodeGuid = null;
                s_activeSession.CurrentRunner.Enter(s_activeSession);
            }

            s_activeSession.CurrentRunner.Update(s_activeSession, deltaTime);
            if (s_activeSession.ShouldTerminateInteraction)
            {
                s_activeSession.CurrentRunner.Exit(s_activeSession);
                s_activeSession.CurrentRunner = null;
                FinishSession(wasCancelled: false);
                return;
            }

            if (!s_activeSession.CurrentRunner.IsCompleted(s_activeSession))
                return;

            s_activeSession.CurrentRunner.Exit(s_activeSession);
            s_activeSession.CurrentRunner = null;
            s_activeSession.CurrentNodeGuid = ResolveNextNodeGuid(s_activeSession, currentNode, s_activeSession.SelectedNextNodeGuid);
            s_activeSession.SelectedNextNodeGuid = null;
        }
    }

    private static void FinishSession(bool wasCancelled)
    {
        if (s_activeSession == null)
            return;

        if (wasCancelled)
            s_activeSession.Cancel();

        s_activeSession = null;
    }

    private static string ResolveNextNodeGuid(NPCInteractionSession session, NPCInteractionNodeData currentNode, string selectedNextNodeGuid)
    {
        if (!string.IsNullOrWhiteSpace(selectedNextNodeGuid))
            return selectedNextNodeGuid;

        if (currentNode?.Branches != null)
        {
            for (int i = 0; i < currentNode.Branches.Count; i++)
            {
                NPCInteractionBranchData branch = currentNode.Branches[i];
                if (branch != null && branch.IsEnabled())
                    return branch.NextNodeGuid;
            }
        }

        return null;
    }

    private static NPCInteractionNodeFactory CreateFactory()
    {
        NPCInteractionNodeFactory factory = new NPCInteractionNodeFactory();
        NPCInteractionNodeRegistry.RegisterAll(factory);
        return factory;
    }
}
