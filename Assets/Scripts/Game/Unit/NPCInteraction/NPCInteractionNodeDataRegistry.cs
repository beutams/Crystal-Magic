using System;
using System.Collections.Generic;

namespace CrystalMagic.Game.Data
{
    public static class NPCInteractionNodeDataRegistry
    {
        private static readonly NPCInteractionNodeDataFactory s_factory = CreateFactory();

        public static IReadOnlyList<string> TypeOrder => NPCInteractionNodeRegistry.TypeOrder;

        public static bool TryGetNodeType(string typeName, out Type nodeType)
        {
            return NPCInteractionNodeRegistry.TryGetNodeType(typeName, out nodeType);
        }

        public static string GetDisplayName(string typeName)
        {
            return NPCInteractionNodeRegistry.GetDisplayName(typeName);
        }

        public static string ResolveTypeName(NPCInteractionNodeData node)
        {
            if (node == null)
            {
                return DefaultTypeName;
            }

            if (NPCInteractionNodeRegistry.TryGetNodeKey(node.GetType(), out string typeName))
            {
                return typeName;
            }

            return DefaultTypeName;
        }

        public static NPCInteractionNodeData Create(string typeName)
        {
            if (!NPCInteractionNodeRegistry.ContainsKey(typeName))
            {
                typeName = DefaultTypeName;
            }

            return s_factory.CreateNode(typeName);
        }

        public static string GetSummary(NPCInteractionNodeData node)
        {
            string typeName = ResolveTypeName(node);
            string displayName = GetDisplayName(typeName);

            return node switch
            {
                NPCDialogueInteractionNodeData dialogue => $"{displayName} | {(string.IsNullOrWhiteSpace(dialogue.ContentKey) ? "Empty" : dialogue.ContentKey)}",
                NPCSelectInteractionNodeData select => $"{displayName} | {select.Options?.Count ?? 0} option(s)",
                NPCOpenUIInteractionNodeData openUI => $"{displayName} | {(string.IsNullOrWhiteSpace(openUI.UIName) ? "Empty" : openUI.UIName)}",
                NPCMoveInteractionNodeData move => $"{displayName} | {(string.IsNullOrWhiteSpace(move.TargetMarker) ? "Empty" : move.TargetMarker)}",
                NPCEnterDungeonInteractionNodeData enterDungeon => $"{displayName} | Floor {Math.Max(1, enterDungeon.DungeonFloor)}",
                NPCEnterTrainingGroundInteractionNodeData => displayName,
                _ => displayName,
            };
        }

        private static string DefaultTypeName =>
            string.IsNullOrWhiteSpace(NPCInteractionNodeRegistry.DefaultTypeKey)
                ? (TypeOrder.Count > 0 ? TypeOrder[0] : "Dialogue")
                : NPCInteractionNodeRegistry.DefaultTypeKey;

        private static NPCInteractionNodeDataFactory CreateFactory()
        {
            var factory = new NPCInteractionNodeDataFactory();
            NPCInteractionNodeRegistry.RegisterAll(factory);
            return factory;
        }
    }
}
