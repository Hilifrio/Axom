using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Dessin immédiat : chaque frame, toutes les formes sont accumulées dans un seul mesh dynamique (1 draw call).
public class NeonRenderer : MonoBehaviour
{
    const int ArcSegmentDegrees = 3;
    const float GlowRatio = 3.2f;

    readonly List<Vector3> _vertices = new List<Vector3>(16384);
    readonly List<Color32> _colors = new List<Color32>(16384);
    readonly List<Vector3> _uvs = new List<Vector3>(16384);
    readonly List<int> _indices = new List<int>(32768);
    Mesh _mesh;

    void Awake()
    {
        _mesh = new Mesh { name = "Neon", indexFormat = IndexFormat.UInt32 };
        _mesh.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
        MeshRenderer meshRenderer = gameObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = new Material(Resources.Load<Shader>("AxomNeon"));
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
    }

    void OnDestroy()
    {
        Destroy(_mesh);
    }

    public void Clear()
    {
        _vertices.Clear();
        _colors.Clear();
        _uvs.Clear();
        _indices.Clear();
    }

    public void Apply()
    {
        _mesh.Clear(true);
        _mesh.SetVertices(_vertices);
        _mesh.SetColors(_colors);
        _mesh.SetUVs(0, _uvs);
        _mesh.SetTriangles(_indices, 0, false);
        _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 200f);
    }

    public void Line(Vector2 from, Vector2 to, float thickness, Color color, float glow = GlowRatio)
    {
        Vector2 direction = to - from;
        float length = direction.magnitude;
        if (length < 0.0001f) return;
        direction /= length;

        float half = thickness * 0.5f * glow;
        float core = 1f / glow;
        Vector2 normal = new Vector2(-direction.y, direction.x) * half;
        Vector2 start = from - direction * thickness * 0.5f;
        Vector2 end = to + direction * thickness * 0.5f;
        Color32 color32 = color;

        int index = _vertices.Count;
        AddVertex(start - normal, color32, new Vector3(0f, -1f, core));
        AddVertex(start + normal, color32, new Vector3(0f, 1f, core));
        AddVertex(end + normal, color32, new Vector3(0f, 1f, core));
        AddVertex(end - normal, color32, new Vector3(0f, -1f, core));
        AddQuad(index);
    }

    public void Arc(Vector2 center, float radius, float fromDegrees, float toDegrees, float thickness, Color color, float glow = GlowRatio)
    {
        float span = toDegrees - fromDegrees;
        int segments = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(span) / ArcSegmentDegrees));
        float half = thickness * 0.5f * glow;
        float core = 1f / glow;
        Color32 color32 = color;

        int index = _vertices.Count;
        for (int i = 0; i <= segments; i++)
        {
            float angle = (fromDegrees + span * i / segments) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            AddVertex(center + direction * (radius - half), color32, new Vector3(0f, -1f, core));
            AddVertex(center + direction * (radius + half), color32, new Vector3(0f, 1f, core));
            if (i < segments) AddQuad(index + i * 2, index + i * 2 + 1, index + i * 2 + 3, index + i * 2 + 2);
        }
    }

    public void Disc(Vector2 center, float radius, Color color, float glow = GlowRatio)
    {
        float half = radius * glow;
        float core = 1f / glow;
        Color32 color32 = color;

        int index = _vertices.Count;
        AddVertex(center + new Vector2(-half, -half), color32, new Vector3(-1f, -1f, core));
        AddVertex(center + new Vector2(-half, half), color32, new Vector3(-1f, 1f, core));
        AddVertex(center + new Vector2(half, half), color32, new Vector3(1f, 1f, core));
        AddVertex(center + new Vector2(half, -half), color32, new Vector3(1f, -1f, core));
        AddQuad(index);
    }

    public void Fill(Rect rect, Color color)
    {
        Color32 color32 = color;
        int index = _vertices.Count;
        AddVertex(new Vector2(rect.xMin, rect.yMin), color32, new Vector3(0f, 0f, 2f));
        AddVertex(new Vector2(rect.xMin, rect.yMax), color32, new Vector3(0f, 0f, 2f));
        AddVertex(new Vector2(rect.xMax, rect.yMax), color32, new Vector3(0f, 0f, 2f));
        AddVertex(new Vector2(rect.xMax, rect.yMin), color32, new Vector3(0f, 0f, 2f));
        AddQuad(index);
    }

    public void Text(string text, Vector2 center, float height, Color color, float weight = 0.1f)
    {
        float scale = height / VectorFont.GlyphHeight;
        float width = VectorFont.Measure(text) * scale;
        Vector2 origin = center - new Vector2(width * 0.5f, height * 0.5f);
        float thickness = Mathf.Max(height * weight, 0.02f);

        foreach (char character in text)
        {
            Vector2[][] strokes = VectorFont.Strokes(character);
            if (strokes != null)
            {
                foreach (Vector2[] stroke in strokes)
                    for (int i = 0; i + 1 < stroke.Length; i++)
                        Line(origin + stroke[i] * scale, origin + stroke[i + 1] * scale, thickness, color, 2.4f);
            }
            origin.x += VectorFont.Advance * scale;
        }
    }

    void AddVertex(Vector2 position, Color32 color, Vector3 uv)
    {
        _vertices.Add(position);
        _colors.Add(color);
        _uvs.Add(uv);
    }

    void AddQuad(int index) => AddQuad(index, index + 1, index + 2, index + 3);

    void AddQuad(int a, int b, int c, int d)
    {
        _indices.Add(a);
        _indices.Add(b);
        _indices.Add(c);
        _indices.Add(a);
        _indices.Add(c);
        _indices.Add(d);
    }
}
