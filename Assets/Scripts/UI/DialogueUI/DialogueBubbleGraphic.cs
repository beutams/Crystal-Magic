using UnityEngine;
using UnityEngine.UI;

namespace CrystalMagic.UI
{
    /// <summary>Scalable comic bubble; no raster art or runtime sprite allocation.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class DialogueBubbleGraphic : MaskableGraphic
    {
        [SerializeField] private Color _outline = new(0.12f, 0.15f, 0.17f, 1f);
        [SerializeField] private Color _shadow = new(0.30f, 0.43f, 0.50f, 1f);
        [SerializeField] private float _radius = 20f;
        [SerializeField] private float _border = 2f;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect body = GetPixelAdjustedRect();
            if (body.width <= 4f || body.height <= 4f)
                return;
            Rect shadow = body;
            shadow.position += new Vector2(1f, -6f);
            AddLayer(mesh, shadow, _outline, 0f);
            AddLayer(mesh, Inset(shadow, _border), _shadow, _border);
            AddLayer(mesh, body, _outline, 0f);
            AddLayer(mesh, Inset(body, _border), color, _border);
        }

        private static Rect Inset(Rect rect, float amount) =>
            new(rect.x + amount, rect.y + amount, rect.width - amount * 2f, rect.height - amount * 2f);

        private void AddLayer(VertexHelper mesh, Rect rect, Color tint, float inset)
        {
            const int segments = 8;
            int center = mesh.currentVertCount;
            mesh.AddVert(rect.center, tint, Vector2.zero);
            float radius = Mathf.Min(Mathf.Max(1f, _radius - inset), Mathf.Min(rect.width, rect.height) * 0.5f);
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 origin = corner switch
                {
                    0 => new Vector2(rect.xMax - radius, rect.yMax - radius),
                    1 => new Vector2(rect.xMin + radius, rect.yMax - radius),
                    2 => new Vector2(rect.xMin + radius, rect.yMin + radius),
                    _ => new Vector2(rect.xMax - radius, rect.yMin + radius),
                };
                for (int i = 0; i <= segments; i++)
                {
                    float angle = (corner * 90f + i * 90f / segments) * Mathf.Deg2Rad;
                    mesh.AddVert(origin + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, tint, Vector2.zero);
                }
            }
            int count = 4 * (segments + 1);
            for (int i = 0; i < count; i++)
                mesh.AddTriangle(center, center + 1 + i, center + 1 + (i + 1) % count);

            // The tail overlaps the body slightly, removing the bottom border at its mouth.
            int tail = mesh.currentVertCount;
            float middle = rect.center.x;
            mesh.AddVert(new Vector2(middle + 3f + inset, rect.yMin + _border + 1f), tint, Vector2.zero);
            mesh.AddVert(new Vector2(middle - 8f + inset, rect.yMin - 20f + inset), tint, Vector2.zero);
            mesh.AddVert(new Vector2(middle + 33f - inset, rect.yMin + _border + 1f), tint, Vector2.zero);
            mesh.AddTriangle(tail, tail + 1, tail + 2);
        }
    }
}
