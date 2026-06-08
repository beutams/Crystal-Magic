// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Use menu: Tools/Registry/NPC Interaction Node

using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;

public static class NPCInteractionNodeRegistry
{
    private static readonly string[] s_typeOrder =
    {
        "Dialogue",
        "Select",
        "OpenUI",
        "Move",
        "EnterDungeon",
        "EnterTrainingGround",
        "EnterTown",
    };

    private static readonly Dictionary<string, Type> s_types = new(StringComparer.Ordinal)
    {
        { "Dialogue", typeof(CrystalMagic.Game.Data.NPCDialogueInteractionNodeData) },
        { "Select", typeof(CrystalMagic.Game.Data.NPCSelectInteractionNodeData) },
        { "OpenUI", typeof(CrystalMagic.Game.Data.NPCOpenUIInteractionNodeData) },
        { "Move", typeof(CrystalMagic.Game.Data.NPCMoveInteractionNodeData) },
        { "EnterDungeon", typeof(CrystalMagic.Game.Data.NPCEnterDungeonInteractionNodeData) },
        { "EnterTrainingGround", typeof(CrystalMagic.Game.Data.NPCEnterTrainingGroundInteractionNodeData) },
        { "EnterTown", typeof(CrystalMagic.Game.Data.NPCEnterTownInteractionNodeData) },
    };

    private static readonly Dictionary<Type, string> s_keys = new()
    {
        { typeof(CrystalMagic.Game.Data.NPCDialogueInteractionNodeData), "Dialogue" },
        { typeof(CrystalMagic.Game.Data.NPCSelectInteractionNodeData), "Select" },
        { typeof(CrystalMagic.Game.Data.NPCOpenUIInteractionNodeData), "OpenUI" },
        { typeof(CrystalMagic.Game.Data.NPCMoveInteractionNodeData), "Move" },
        { typeof(CrystalMagic.Game.Data.NPCEnterDungeonInteractionNodeData), "EnterDungeon" },
        { typeof(CrystalMagic.Game.Data.NPCEnterTrainingGroundInteractionNodeData), "EnterTrainingGround" },
        { typeof(CrystalMagic.Game.Data.NPCEnterTownInteractionNodeData), "EnterTown" },
    };

    private static readonly Dictionary<string, string> s_displayNames = new(StringComparer.Ordinal)
    {
        { "Dialogue", "Dialogue" },
        { "Select", "Select" },
        { "OpenUI", "Open UI" },
        { "Move", "Move" },
        { "EnterDungeon", "Enter Dungeon" },
        { "EnterTrainingGround", "Enter Training Ground" },
        { "EnterTown", "Enter Town" },
    };

    public static string DefaultTypeKey => "Dialogue";

    public static IReadOnlyList<string> TypeOrder => s_typeOrder;

    public static bool ContainsKey(string key)
    {
        return s_types.ContainsKey(key ?? string.Empty);
    }

    public static bool TryGetNodeType(string key, out Type type)
    {
        return s_types.TryGetValue(key ?? string.Empty, out type);
    }

    public static bool TryGetNodeKey(Type type, out string key)
    {
        return s_keys.TryGetValue(type, out key);
    }

    public static string GetDisplayName(string key)
    {
        return s_displayNames.TryGetValue(key ?? string.Empty, out string displayName)
            ? displayName
            : key ?? "Unknown";
    }

    public static void RegisterAll(NPCInteractionNodeDataFactory factory)
    {
        if (factory == null)
            return;

        factory.Register("Dialogue", static () => new CrystalMagic.Game.Data.NPCDialogueInteractionNodeData());
        factory.Register("Select", static () => new CrystalMagic.Game.Data.NPCSelectInteractionNodeData());
        factory.Register("OpenUI", static () => new CrystalMagic.Game.Data.NPCOpenUIInteractionNodeData());
        factory.Register("Move", static () => new CrystalMagic.Game.Data.NPCMoveInteractionNodeData());
        factory.Register("EnterDungeon", static () => new CrystalMagic.Game.Data.NPCEnterDungeonInteractionNodeData());
        factory.Register("EnterTrainingGround", static () => new CrystalMagic.Game.Data.NPCEnterTrainingGroundInteractionNodeData());
        factory.Register("EnterTown", static () => new CrystalMagic.Game.Data.NPCEnterTownInteractionNodeData());
    }

    public static void RegisterAll(NPCInteractionNodeFactory factory)
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
