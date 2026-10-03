using System;
using System.Collections.Generic;
using UnityEngine;

namespace CrystalMagic.Editor.EffectGraph
{
    internal static class EffectGraphAutoLayout
    {
        public const float ContainerWidth = 420f;
        public const float HeaderHeight = 110f;
        public const float EffectRowHeight = 150f;
        public const float EffectWidth = 376f;
        public const float EffectGap = 80f;
        public const float ContainerPadding = 44f;

        public static float GetWidth(int effectCount) => Mathf.Max(ContainerWidth,
            ContainerPadding + effectCount * EffectWidth + Mathf.Max(0, effectCount - 1) * EffectGap);

        public static float GetHeight(int effectCount) => HeaderHeight + (effectCount > 0 ? EffectRowHeight : 0f);

        public static void Arrange(EffectGraphModel model, EffectGraphLayoutData layout,
            Func<EffectGraphContainerModel, Vector2> measuredSize = null)
        {
            HashSet<string> savedPaths = new();
            foreach (var saved in layout.Containers)
                if (saved != null) savedPaths.Add(saved.Path);
            List<OrderedGraphLayout.Node> nodes = new();
            List<OrderedGraphLayout.Edge> edges = new();
            foreach (EffectGraphContainerModel container in model.Containers)
            {
                if (!container.IsRoot && container.Effects.Length == 0 && !savedPaths.Contains(container.Path))
                    continue;
                Vector2 size = measuredSize?.Invoke(container) ?? Vector2.zero;
                nodes.Add(new(container.Path, Mathf.Max(GetWidth(container.Effects.Length), size.x),
                    Mathf.Max(GetHeight(container.Effects.Length), size.y)));
                if (container.Parent != null)
                    edges.Add(new(container.Parent.Path, container.Path));
            }
            var result = OrderedGraphLayout.Arrange(nodes, edges, "root", true, originY: 220f);
            layout.Containers.Clear();
            foreach (var node in nodes)
            {
                var position = result.Positions[node.Id];
                layout.Containers.Add(new EffectGraphContainerLayout
                {
                    Path = node.Id, Position = new Vector2(position.X, position.Y), Expanded = true,
                });
            }
            var root = result.Positions["root"];
            layout.ViewPosition = new Vector2(240f - root.X - GetWidth(model.Root.Effects.Length) * 0.5f, 0f);
            layout.ViewScale = 1f;
        }
    }
}
