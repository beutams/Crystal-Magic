using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;

public readonly struct NPCInteractionNodeStartedEvent : IGameEvent
{
    public NPCInteractionNodeStartedEvent(Entity target, NPCData npcData, NPCInteractionData interaction, NPCInteractionNodeData node)
    {
        Target = target;
        NpcData = npcData;
        Interaction = interaction;
        Node = node;
    }

    public Entity Target { get; }
    public NPCData NpcData { get; }
    public NPCInteractionData Interaction { get; }
    public NPCInteractionNodeData Node { get; }
}
