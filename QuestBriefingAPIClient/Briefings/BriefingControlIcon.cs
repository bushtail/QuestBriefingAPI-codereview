using UnityEngine;
using UnityEngine.UI;

namespace Manimal.QuestBriefingAPI.Briefings;

public sealed class BriefingControlIcon : MaskableGraphic
{
    public enum Symbol { Play, Stop, Replay }
    
    public Symbol Shape;

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = GetPixelAdjustedRect();
        var center = rect.center;
        var radius = Mathf.Min(rect.width, rect.height) * 0.38f;
        switch (Shape)
        {
            case Symbol.Play:
            {
                Triangle(mesh, center + new Vector2(-radius * 0.6f, -radius),
                    center + new Vector2(-radius * 0.6f, radius), center + new Vector2(radius, 0));
                break;
            }
            
            case Symbol.Stop:
            {
                var r = radius * 0.78f;
                Quad(mesh, center + new Vector2(-r, -r), center + new Vector2(-r, r),
                    center + new Vector2(r, r), center + new Vector2(r, -r));
                break;
            }

            default:
            {
                const int segments = 28;
                var inner = radius * 0.76f;
                
                for (var i = 0; i < segments; i++)
                {
                    var a = Mathf.Lerp(45, 315, (float)i / segments) * Mathf.Deg2Rad;
                    var b = Mathf.Lerp(45, 315, (float)(i + 1) / segments) * Mathf.Deg2Rad;
                    Quad(mesh, center + Point(a, inner), center + Point(a, radius),
                        center + Point(b, radius), center + Point(b, inner));
                }
                
                var tip = center + Point(45 * Mathf.Deg2Rad, radius);
                Triangle(mesh, tip + new Vector2(radius * 0.12f, radius * 0.3f),
                    tip + new Vector2(-radius * 0.5f, radius * 0.25f),
                    tip + new Vector2(radius * 0.05f, -radius * 0.38f));
                
                break;
            }
        }
    }

    private static Vector2 Point(float angle, float radius) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    
    private void Triangle(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c)
    {
        var offset = mesh.currentVertCount;
        mesh.AddVert(a, color, Vector2.zero);
        mesh.AddVert(b, color, Vector2.zero);
        mesh.AddVert(c, color, Vector2.zero);
        mesh.AddTriangle(offset, offset + 1, offset + 2);
    }
    
    private void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        var offset = mesh.currentVertCount;
        mesh.AddVert(a, color, Vector2.zero);
        mesh.AddVert(b, color, Vector2.zero);
        mesh.AddVert(c, color, Vector2.zero);
        mesh.AddVert(d, color, Vector2.zero);
        mesh.AddTriangle(offset, offset + 1, offset + 2);
        mesh.AddTriangle(offset, offset + 2, offset + 3);
    }
}