using System;
using System.Collections.Generic;

namespace CrystalMagic.Editor
{
    /// <summary>
    /// Layout only: keeps declared branch order, reserves whole subtree lanes and advances
    /// every forward edge by at least one layer. DFS back edges remain backwards as loops.
    /// Pure managed code so batch migration and editor layout use exactly the same algorithm.
    /// </summary>
    public static class OrderedGraphLayout
    {
        public sealed class Node
        {
            public string Id;
            public float Width;
            public float Height;
            public Node(string id, float width, float height)
            {
                Id = id;
                Width = Math.Max(1, width);
                Height = Math.Max(1, height);
            }
        }

        public readonly struct Edge
        {
            public readonly string From;
            public readonly string To;
            public Edge(string from, string to) { From = from; To = to; }
        }

        public readonly struct Position
        {
            public readonly float X;
            public readonly float Y;
            public Position(float x, float y) { X = x; Y = y; }
        }

        public sealed class Result
        {
            public readonly Dictionary<string, Position> Positions = new(StringComparer.Ordinal);
            public readonly Dictionary<string, int> Depths = new(StringComparer.Ordinal);
            public readonly List<Edge> BackEdges = new();
        }

        private sealed class WorkNode
        {
            public Node Data;
            public readonly List<WorkNode> Outputs = new();
            public readonly List<WorkNode> Forward = new();
            public readonly List<WorkNode> Children = new();
            public int Incoming, Color, Depth;
            public float Span;
        }

        public static Result Arrange(IReadOnlyList<Node> nodes, IReadOnlyList<Edge> edges,
            string entryId, bool topToBottom, float layerGap = 140f, float siblingGap = 90f,
            float originX = 80f, float originY = 80f)
        {
            Result result = new();
            Dictionary<string, WorkNode> byId = new(StringComparer.Ordinal);
            List<WorkNode> ordered = new();
            foreach (Node node in nodes)
            {
                if (node == null || string.IsNullOrEmpty(node.Id) || byId.ContainsKey(node.Id))
                    continue;
                WorkNode work = new() { Data = node };
                byId.Add(node.Id, work);
                ordered.Add(work);
            }
            foreach (Edge edge in edges)
            {
                if (edge.From == null || edge.To == null ||
                    !byId.TryGetValue(edge.From, out WorkNode from) || !byId.TryGetValue(edge.To, out WorkNode to) ||
                    from.Outputs.Contains(to))
                    continue;
                from.Outputs.Add(to);
                to.Incoming++;
            }

            List<WorkNode> roots = new();
            List<WorkNode> postorder = new();
            void Visit(WorkNode node)
            {
                node.Color = 1;
                foreach (WorkNode child in node.Outputs)
                {
                    if (child.Color == 1)
                    {
                        result.BackEdges.Add(new Edge(node.Data.Id, child.Data.Id));
                        continue;
                    }
                    node.Forward.Add(child);
                    if (child.Color == 0)
                    {
                        node.Children.Add(child);
                        Visit(child);
                    }
                }
                node.Color = 2;
                postorder.Add(node);
            }
            void VisitRoot(WorkNode root)
            {
                if (root.Color != 0) return;
                roots.Add(root);
                Visit(root);
            }
            if (entryId != null && byId.TryGetValue(entryId, out WorkNode entry))
                VisitRoot(entry);
            foreach (WorkNode node in ordered)
                if (node.Incoming == 0) VisitRoot(node);
            foreach (WorkNode node in ordered)
                VisitRoot(node); // Disconnected nodes and disconnected cycles remain visible.

            // Longest forward path puts a merge AFTER all its inputs, unlike plain BFS.
            int maxDepth = 0;
            for (int i = postorder.Count - 1; i >= 0; i--)
            {
                WorkNode node = postorder[i];
                foreach (WorkNode child in node.Forward)
                    child.Depth = Math.Max(child.Depth, node.Depth + 1);
                maxDepth = Math.Max(maxDepth, node.Depth);
            }
            float[] layerSizes = new float[maxDepth + 1];
            float[] layerStarts = new float[maxDepth + 1];
            foreach (WorkNode node in ordered)
                layerSizes[node.Depth] = Math.Max(layerSizes[node.Depth], topToBottom ? node.Data.Height : node.Data.Width);
            for (int i = 1; i < layerStarts.Length; i++)
                layerStarts[i] = layerStarts[i - 1] + layerSizes[i - 1] + layerGap;

            foreach (WorkNode node in postorder)
            {
                float childrenSpan = 0;
                foreach (WorkNode child in node.Children)
                    childrenSpan += child.Span;
                childrenSpan += Math.Max(0, node.Children.Count - 1) * siblingGap;
                node.Span = Math.Max(topToBottom ? node.Data.Width : node.Data.Height, childrenSpan);
            }
            void Place(WorkNode node, float laneStart)
            {
                float crossSize = topToBottom ? node.Data.Width : node.Data.Height;
                float cross = laneStart + (node.Span - crossSize) * 0.5f;
                float along = layerStarts[node.Depth];
                result.Positions[node.Data.Id] = topToBottom
                    ? new Position(originX + cross, originY + along)
                    : new Position(originX + along, originY + cross);
                result.Depths[node.Data.Id] = node.Depth;
                float childrenSpan = 0;
                foreach (WorkNode child in node.Children) childrenSpan += child.Span;
                childrenSpan += Math.Max(0, node.Children.Count - 1) * siblingGap;
                float cursor = laneStart + (node.Span - childrenSpan) * 0.5f;
                foreach (WorkNode child in node.Children)
                {
                    Place(child, cursor);
                    cursor += child.Span + siblingGap;
                }
            }
            float lane = 0;
            foreach (WorkNode root in roots)
            {
                Place(root, lane);
                lane += root.Span + siblingGap * 2;
            }

            // A behavior DAG can share a leaf (e.g. FaceTarget) between several sequences.
            // Its single visual instance must follow every sequence's earlier siblings and
            // precede every later sibling, not simply stay in the first parent's lane.
            if (topToBottom)
            {
                for (int depth = 0; depth <= maxDepth; depth++)
                {
                    List<WorkNode> layer = ordered.FindAll(node => node.Depth == depth);
                    if (!layer.Exists(node => node.Incoming > 1)) continue;
                    layer.Sort((a, b) => result.Positions[a.Data.Id].X.CompareTo(result.Positions[b.Data.Id].X));
                    Dictionary<WorkNode, List<WorkNode>> after = new();
                    Dictionary<WorkNode, int> incoming = new();
                    foreach (WorkNode node in layer) { after[node] = new(); incoming[node] = 0; }
                    foreach (WorkNode parent in ordered)
                    {
                        WorkNode previous = null;
                        foreach (WorkNode child in parent.Outputs)
                        {
                            if (child.Depth != depth) continue;
                            if (previous != null && !after[previous].Contains(child))
                            {
                                after[previous].Add(child);
                                incoming[child]++;
                            }
                            previous = child;
                        }
                    }
                    float cursor = originX;
                    List<WorkNode> remaining = new(layer);
                    while (remaining.Count > 0)
                    {
                        WorkNode next = remaining.Find(node => incoming[node] == 0);
                        if (next == null)
                            throw new InvalidOperationException("Shared nodes have contradictory sibling execution orders.");
                        Position old = result.Positions[next.Data.Id];
                        result.Positions[next.Data.Id] = new Position(cursor, old.Y);
                        cursor += next.Data.Width + siblingGap;
                        remaining.Remove(next);
                        foreach (WorkNode child in after[next]) incoming[child]--;
                    }
                }
            }
            return result;
        }
    }
}
