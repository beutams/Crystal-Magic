using CrystalMagic.Core;
using CrystalMagic.Game.Data;
using Unity.Entities;

public readonly struct NPCInteractionFinishedEvent : IGameEvent
{
    public NPCInteractionFinishedEvent(Entity target, NPCData npcData, NPCInteractionData interaction, bool wasCancelled)
    {
        Target = target;
        NpcData = npcData;
        Interaction = interaction;
        WasCancelled = wasCancelled;
    }

    public Entity Target { get; }
    public NPCData NpcData { get; }
    public NPCInteractionData Interaction { get; }
    public bool WasCancelled { get; }
}
