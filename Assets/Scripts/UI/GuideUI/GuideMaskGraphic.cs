using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Union-of-rectangles mask. Both ends of a drag stay raycast-transparent.</summary>
public sealed class GuideMaskGraphic : MaskableGraphic
{
    private Rect[] _holes = System.Array.Empty<Rect>();
    private bool _block;
    public void Render(Rect[] holes, bool block) { _holes = holes; _block = block; raycastTarget = block; SetVerticesDirty(); }
    public override bool Raycast(Vector2 screenPoint, Camera eventCamera)
    {
        if (!_block || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out Vector2 point)) return false;
        foreach (Rect hole in _holes) if (hole.Contains(point)) return false;
        return base.Raycast(screenPoint, eventCamera);
    }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect bounds = rectTransform.rect;
        if (_block)
        {
            var xs = new List<float> { bounds.xMin, bounds.xMax };
            var ys = new List<float> { bounds.yMin, bounds.yMax };
            foreach (Rect hole in _holes)
            {
                xs.Add(Mathf.Clamp(hole.xMin, bounds.xMin, bounds.xMax)); xs.Add(Mathf.Clamp(hole.xMax, bounds.xMin, bounds.xMax));
                ys.Add(Mathf.Clamp(hole.yMin, bounds.yMin, bounds.yMax)); ys.Add(Mathf.Clamp(hole.yMax, bounds.yMin, bounds.yMax));
            }
            xs.Sort(); ys.Sort();
            for (int x = 1; x < xs.Count; x++) for (int y = 1; y < ys.Count; y++)
            {
                Rect cell = Rect.MinMaxRect(xs[x - 1], ys[y - 1], xs[x], ys[y]);
                bool inside = false;
                foreach (Rect hole in _holes) if (hole.Contains(cell.center)) { inside = true; break; }
                if (!inside) Quad(vh, cell, new Color(.035f, .05f, .07f, .68f));
            }
        }
        Color border = new(1f, .76f, .2f, 1);
        foreach (Rect hole in _holes)
        {
            Quad(vh, new Rect(hole.xMin - 3, hole.yMin - 3, hole.width + 6, 3), border);
            Quad(vh, new Rect(hole.xMin - 3, hole.yMax, hole.width + 6, 3), border);
            Quad(vh, new Rect(hole.xMin - 3, hole.yMin, 3, hole.height), border);
            Quad(vh, new Rect(hole.xMax, hole.yMin, 3, hole.height), border);
        }
        if (_holes.Length == 2 && _holes[0] != _holes[1])
        {
            Vector2 start = _holes[0].center, end = _holes[1].center;
            Vector2 direction = (end - start).normalized;
            Line(vh, start, end, border);
            Vector2 normal = new(-direction.y, direction.x);
            Line(vh, end, end - direction * 22 + normal * 13, border);
            Line(vh, end, end - direction * 22 - normal * 13, border);
        }
    }
    private static void Line(VertexHelper vh, Vector2 from, Vector2 to, Color color)
    {
        Vector2 direction = (to - from).normalized;
        Vector2 side = new Vector2(-direction.y, direction.x) * 2;
        AddQuad(vh, from - side, from + side, to + side, to - side, color);
    }
    private static void Quad(VertexHelper vh, Rect rect, Color color)
    {
        if (rect.width <= 0 || rect.height <= 0) return;
        AddQuad(vh, new(rect.xMin, rect.yMin), new(rect.xMin, rect.yMax), new(rect.xMax, rect.yMax), new(rect.xMax, rect.yMin), color);
    }
    private static void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int i = vh.currentVertCount;
        vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero);
        vh.AddVert(c, color, Vector2.zero); vh.AddVert(d, color, Vector2.zero);
        vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i + 2, i + 3, i);
    }
}
