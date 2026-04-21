// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Use menu: Tools/NPC Interaction/Generate Node Registry

using CrystalMagic.Game.Data;

public static class NPCInteractionNodeRegistry
{
    public static void RegisterAll(NPCInteractionNodeFactory factory)
    {
        factory.Register<NPCDialogueInteractionNodeData>(node => new NPCDialogueInteractionNodeRunner(node));
        factory.Register<NPCEnterDungeonInteractionNodeData>(node => new NPCEnterDungeonInteractionNodeRunner(node));
        factory.Register<NPCEnterTrainingGroundInteractionNodeData>(node => new NPCEnterTrainingGroundInteractionNodeRunner(node));
        factory.Register<NPCMoveInteractionNodeData>(node => new NPCMoveInteractionNodeRunner(node));
        factory.Register<NPCOpenUIInteractionNodeData>(node => new NPCOpenUIInteractionNodeRunner(node));
        factory.Register<NPCSelectInteractionNodeData>(node => new NPCSelectInteractionNodeRunner(node));
    }
}
