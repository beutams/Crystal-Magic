using CrystalMagic.Game.Data;
using Unity.Entities;

public sealed class NPCInteractionSession
{
    public NPCInteractionSession(Entity target, NPCData npcData, NPCInteractionData interaction)
    {
        Target = target;
        NpcData = npcData;
        Interaction = interaction;
        CurrentNodeGuid = interaction?.EntryNodeGuid;
        IsActive = true;
    }

    public Entity Target { get; }
    public NPCData NpcData { get; }
    public NPCInteractionData Interaction { get; }
    public string CurrentNodeGuid { get; set; }
    public string SelectedNextNodeGuid { get; set; }
    public NPCInteractionNodeRunner CurrentRunner { get; set; }
    public bool IsActive { get; private set; }
    public bool ShouldTerminateInteraction { get; private set; }

    public NPCInteractionNodeData GetCurrentNode()
    {
        return Interaction?.GetNode(CurrentNodeGuid);
    }

    public bool IsTargetValid(EntityManager entityManager)
    {
        return Target != Entity.Null && entityManager.Exists(Target);
    }

    public void Cancel()
    {
        if (!IsActive)
        {
            return;
        }

        CurrentRunner?.Cancel(this);
        CurrentRunner = null;
        IsActive = false;
    }

    public void RequestTerminateInteraction()
    {
        ShouldTerminateInteraction = true;
    }
}
