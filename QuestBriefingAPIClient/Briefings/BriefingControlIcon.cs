using UnityEngine;
using UnityEngine.UI;

namespace Manimal.QuestBriefingAPI
{
    // Geometry avoids missing symbol glyphs in Tarkov's condensed UI font.
    public sealed class BriefingControlIcon : MaskableGraphic
    {
        public enum Symbol { Play, Stop, Replay }
        public Symbol Shape;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) * 0.38f;
            if (Shape == Symbol.Play)
            {
                Triangle(mesh, center + new Vector2(-radius * 0.6f, -radius),
                    center + new Vector2(-radius * 0.6f, radius), center + new Vector2(radius, 0));
            }
            else if (Shape == Symbol.Stop)
            {
                float r = radius * 0.78f;
                Quad(mesh, center + new Vector2(-r, -r), center + new Vector2(-r, r),
                    center + new Vector2(r, r), center + new Vector2(r, -r));
            }
            else
            {
                const int segments = 28;
                float inner = radius * 0.76f;
                for (int i = 0; i < segments; i++)
                {
                    float a = Mathf.Lerp(45, 315, (float)i / segments) * Mathf.Deg2Rad;
                    float b = Mathf.Lerp(45, 315, (float)(i + 1) / segments) * Mathf.Deg2Rad;
                    Quad(mesh, center + Point(a, inner), center + Point(a, radius),
                        center + Point(b, radius), center + Point(b, inner));
                }
                Vector2 tip = center + Point(45 * Mathf.Deg2Rad, radius);
                Triangle(mesh, tip + new Vector2(radius * 0.12f, radius * 0.3f),
                    tip + new Vector2(-radius * 0.5f, radius * 0.25f),
                    tip + new Vector2(radius * 0.05f, -radius * 0.38f));
            }
        }

        private static Vector2 Point(float angle, float radius) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c)
        {
            int offset = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero);
            mesh.AddVert(b, color, Vector2.zero);
            mesh.AddVert(c, color, Vector2.zero);
            mesh.AddTriangle(offset, offset + 1, offset + 2);
        }
        private void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            int offset = mesh.currentVertCount;
            mesh.AddVert(a, color, Vector2.zero);
            mesh.AddVert(b, color, Vector2.zero);
            mesh.AddVert(c, color, Vector2.zero);
            mesh.AddVert(d, color, Vector2.zero);
            mesh.AddTriangle(offset, offset + 1, offset + 2);
            mesh.AddTriangle(offset, offset + 2, offset + 3);
        }
    }
}
