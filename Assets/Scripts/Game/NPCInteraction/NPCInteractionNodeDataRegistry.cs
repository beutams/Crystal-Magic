// AUTO-GENERATED - DO NOT EDIT MANUALLY
// Use menu: Tools/Registry/NPC Interaction Node

using System;
using System.Collections.Generic;

namespace CrystalMagic.Game.Data
{
    public static class NPCInteractionNodeDataRegistry
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
    }
}
