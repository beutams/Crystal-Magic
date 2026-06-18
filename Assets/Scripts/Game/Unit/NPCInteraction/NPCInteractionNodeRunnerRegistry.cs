// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Use menu: Tools/Registry/NPC Interaction Node

using System;
using CrystalMagic.Game.Data;

public static class NPCInteractionNodeRunnerRegistry
{
    public static void RegisterAll(NPCInteractionNodeRunnerFactory factory)
    {
        if (factory == null)
            return;

        factory.Register(typeof(CrystalMagic.Game.Data.NPCDialogueInteractionNodeData), static node => new NPCDialogueInteractionNodeRunner((CrystalMagic.Game.Data.NPCDialogueInteractionNodeData)node));
        factory.Register(typeof(CrystalMagic.Game.Data.NPCSelectInteractionNodeData), static node => new NPCSelectInteractionNodeRunner((CrystalMagic.Game.Data.NPCSelectInteractionNodeData)node));
        factory.Register(typeof(CrystalMagic.Game.Data.NPCOpenUIInteractionNodeData), static node => new NPCOpenUIInteractionNodeRunner((CrystalMagic.Game.Data.NPCOpenUIInteractionNodeData)node));
        factory.Register(typeof(CrystalMagic.Game.Data.NPCMoveInteractionNodeData), static node => new NPCMoveInteractionNodeRunner((CrystalMagic.Game.Data.NPCMoveInteractionNodeData)node));
        factory.Register(typeof(CrystalMagic.Game.Data.NPCEnterDungeonInteractionNodeData), static node => new NPCEnterDungeonInteractionNodeRunner((CrystalMagic.Game.Data.NPCEnterDungeonInteractionNodeData)node));
        factory.Register(typeof(CrystalMagic.Game.Data.NPCEnterTrainingGroundInteractionNodeData), static node => new NPCEnterTrainingGroundInteractionNodeRunner((CrystalMagic.Game.Data.NPCEnterTrainingGroundInteractionNodeData)node));
        factory.Register(typeof(CrystalMagic.Game.Data.NPCEnterTownInteractionNodeData), static node => new NPCEnterTownInteractionNodeRunner((CrystalMagic.Game.Data.NPCEnterTownInteractionNodeData)node));
    }
}
