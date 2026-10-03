using System;
using System.Collections.Generic;
using CrystalMagic.Game.Data;
using UnityEngine;

namespace CrystalMagic.Editor
{
    public static class UnitGraphAutoLayout
    {
        public const float BehaviorWidth = 260f;
        public const float BehaviorHeight = 140f;
        public const float StateWidth = 360f;
        public const float StateHeight = 320f;

        public static void ArrangeBehaviorTree(BehaviorTreeData tree, Func<string, Vector2> measuredSize = null)
        {
            if (tree?.Nodes == null) return;
            List<OrderedGraphLayout.Node> nodes = new();
            List<OrderedGraphLayout.Edge> edges = new();
            foreach (BehaviorNodeData node in tree.Nodes)
            {
                if (node == null) continue;
                Vector2 size = measuredSize?.Invoke(node.Guid) ?? Vector2.zero;
                nodes.Add(new(node.Guid, Mathf.Max(BehaviorWidth, size.x), Mathf.Max(BehaviorHeight, size.y)));
                if (node.ChildGuids != null)
                    foreach (string child in node.ChildGuids)
                        edges.Add(new(node.Guid, child));
            }
            OrderedGraphLayout.Result layout = OrderedGraphLayout.Arrange(nodes, edges, tree.RootNodeGuid, true);
            foreach (BehaviorNodeData node in tree.Nodes)
                if (node != null && layout.Positions.TryGetValue(node.Guid, out var position))
                    node.EditorPosition = new Vector2(position.X, position.Y);
        }

        public static void ArrangeStateScript(StateScriptInstanceData graph, Func<string, Vector2> measuredSize = null)
        {
            if (graph?.Nodes == null) return;
            List<OrderedGraphLayout.Node> nodes = new();
            List<OrderedGraphLayout.Edge> edges = new();
            foreach (StateScriptNodeData node in graph.Nodes)
            {
                if (node == null) continue;
                Vector2 size = measuredSize?.Invoke(node.Guid) ?? Vector2.zero;
                nodes.Add(new(node.Guid, Mathf.Max(StateWidth, size.x), Mathf.Max(StateHeight, size.y)));
            }
            // Preserve the serialized order of parallel outputs; layout must not change execution.
            if (graph.Edges != null)
                foreach (StateScriptEdgeData edge in graph.Edges)
                    if (edge != null) edges.Add(new(edge.OutputNodeGuid, edge.InputNodeGuid));
            OrderedGraphLayout.Result layout = OrderedGraphLayout.Arrange(nodes, edges, graph.EntryNodeGuid, false);
            foreach (StateScriptNodeData node in graph.Nodes)
                if (node != null && layout.Positions.TryGetValue(node.Guid, out var position))
                    node.EditorPosition = new Vector2(position.X, position.Y);
        }
    }
}
