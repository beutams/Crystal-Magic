using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;

public readonly struct NPCInteractionStartedEvent : IGameEvent
{
    public NPCInteractionStartedEvent(Entity target, NPCData npcData, NPCInteractionData interaction)
    {
        Target = target;
        NpcData = npcData;
        Interaction = interaction;
    }

    public Entity Target { get; }
    public NPCData NpcData { get; }
    public NPCInteractionData Interaction { get; }
}
