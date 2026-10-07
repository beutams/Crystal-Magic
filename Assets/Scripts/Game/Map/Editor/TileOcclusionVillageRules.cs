using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace CrystalMagic.Editor.Map
{
    // Reviewed atlas coordinates, NOT guesses based on TMX layer names.
    // Coordinates below are zero-based 16px tile indices from the top left.
    public static class TileOcclusionVillageRules
    {
        public const string Version = "village-2026-10-05-v1";
        public enum Kind { Ground, Fence, House, Stump, Tree, Hedge, Prop, Review }
        public readonly struct Rule
        {
            public readonly Kind Category;
            public readonly RectInt Template;
            public readonly int ExpectedCells;
            public readonly string Label;
            public Rule(Kind kind, string label = "", RectInt template = default, int count = 0)
            { Category = kind; Label = label; Template = template; ExpectedCells = count; }
        }

        public static Rule Classify(string atlasName, int column, int row, string layerName)
        {
            string atlas = Path.GetFileNameWithoutExtension(atlasName);
            if (atlas == "补充")
            {
                if (column >= 1 && column <= 9 && row >= 3 && row <= 5)
                    return Pattern(Kind.Tree, "树木", 1 + (column - 1) / 3 * 3, 3, 3, 3, 9);
                if (column >= 1 && column <= 9 && row >= 1 && row <= 2)
                    return Pattern(Kind.Tree, "树木", 1 + (column - 1) / 3 * 3, 1, 3, 2, 6);
                return new Rule(Kind.House, "房屋");
            }
            if (atlas == "AltRoofs_Tileset") return new Rule(Kind.House, "房屋");
            if (atlas != "Village_Tileset") return new Rule(Kind.Review, "待复核素材");
            int id = row * 32 + column;
            if (row < 6) return new Rule(Kind.Ground);
            // Ground flowers (including flower sprites placed on non-flower layers).
            if (column >= 3 && column <= 6 && row >= 17 && row <= 20) return new Rule(Kind.Ground, "地表小花");
            if (id == 356 || id == 358) return new Rule(Kind.House, "门窗");
            if (layerName.Contains("屋顶") || layerName == "门") return new Rule(Kind.House, "房屋");
            if (row >= 6 && row <= 9 && column <= 7) return new Rule(Kind.House, "房屋");
            if (row >= 6 && row <= 9 && column >= 8 && column <= 13) return new Rule(Kind.Fence, "栅栏");
            if (row >= 6 && row <= 9 && column >= 14 && column <= 17) return new Rule(Kind.Hedge, "灌木");
            // More stone paving and flat rugs in the right half of the sheet.
            if (row >= 6 && row <= 10 && column >= 20) return new Rule(Kind.Ground);
            if (column >= 30 && row <= 13) return new Rule(Kind.Ground);
            if (id == 417 || id == 448 || id == 449 || id == 450 || id == 481)
                return Pattern(Kind.Stump, "大木桩", 0, 13, 3, 3, 5);
            if (id == 416 || id == 418 || id == 480 || id == 482) return new Rule(Kind.Stump, "小木桩");
            if (column <= 2 && row >= 16 && row <= 17) return Pattern(Kind.Tree, "树木", 0, 16, 3, 2, 6);
            if (column <= 2 && row >= 18 && row <= 20) return Pattern(Kind.Tree, "树木", 0, 18, 3, 3, 9);
            if (column >= 3 && column <= 4 && row >= 14 && row <= 16) return Pattern(Kind.Prop, "水井", 3, 14, 2, 3, 4);
            if (column >= 12 && column <= 13 && row >= 17 && row <= 18) return Pattern(Kind.Prop, "木桶", column, 17, 1, 2, 2);
            if (column >= 15 && column <= 16 && row >= 19 && row <= 20) return Pattern(Kind.Prop, "训练假人", 15, 19, 2, 2, 4);
            if (column >= 15 && column <= 16 && row >= 21 && row <= 22) return Pattern(Kind.Prop, "武器架", 15, 21, 2, 2, 4);
            if (column >= 17 && column <= 18 && row >= 21 && row <= 22) return Pattern(Kind.Prop, "武器架", 17, 21, 2, 2, 4);
            if (column >= 19 && column <= 30 && row >= 21 && row <= 22) return Pattern(Kind.Prop, "炉台", 19 + (column - 19) / 3 * 3, 21, 3, 2, 6);
            if (row >= 22) return new Rule(Kind.House, "房屋");
            if (row >= 10 && row <= 21) return new Rule(Kind.Prop, "箱罐/家具");
            return new Rule(Kind.Review, "待复核素材");
        }

        private static Rule Pattern(Kind kind, string label, int x, int y, int w, int h, int count) =>
            new Rule(kind, label, new RectInt(x, y, w, h), count);

        public sealed class AtlasPixels : IDisposable
        {
            private readonly Dictionary<string, Texture2D> _textures = new();
            public bool HasVisiblePixels(Sprite sprite)
            {
                string path = AssetDatabase.GetAssetPath(sprite.texture);
                if (!File.Exists(path) || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) return true;
                if (!_textures.TryGetValue(path, out Texture2D texture))
                {
                    texture = new Texture2D(2, 2);
                    if (!texture.LoadImage(File.ReadAllBytes(path))) { UnityEngine.Object.DestroyImmediate(texture); return true; }
                    _textures.Add(path, texture);
                }
                Rect r = sprite.rect;
                // Source PNGs only; packed atlas/trimmed rectangles require review.
                if (r.xMax > texture.width || r.yMax > texture.height) return true;
                Color[] pixels = texture.GetPixels((int)r.x, (int)r.y, (int)r.width, (int)r.height);
                foreach (Color pixel in pixels) if (pixel.a > 0.01f) return true;
                return false;
            }
            public void Dispose()
            { foreach (Texture2D texture in _textures.Values) UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
