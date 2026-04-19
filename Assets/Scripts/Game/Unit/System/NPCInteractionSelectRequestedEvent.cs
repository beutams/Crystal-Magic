using System.Collections.Generic;
using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;

public readonly struct NPCInteractionSelectRequestedEvent : IGameEvent
{
    public NPCInteractionSelectRequestedEvent(
        Entity target,
        NPCData npcData,
        NPCInteractionData interaction,
        NPCSelectInteractionNodeData node,
        IReadOnlyList<NPCSelectOptionData> options)
    {
        Target = target;
        NpcData = npcData;
        Interaction = interaction;
        Node = node;
        Options = options;
    }

    public Entity Target { get; }
    public NPCData NpcData { get; }
    public NPCInteractionData Interaction { get; }
    public NPCSelectInteractionNodeData Node { get; }
    public IReadOnlyList<NPCSelectOptionData> Options { get; }
}
