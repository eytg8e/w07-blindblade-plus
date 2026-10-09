using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Rendering;

/// <summary>사진을 참고한 백목 검의 메시를 생성한다. 충돌체나 전투 기능은 생성하지 않는다.</summary>
internal static class PlayerShirasayaGeometry
{
    private const int LENGTH_SEGMENTS = 28;
    private const int WOOD_SIDES = 12;
    private const float BLADE_LENGTH = 0.82f;

    /// <summary>parent 아래에 주어진 mesh와 재질을 사용하는 표현 오브젝트를 생성해 Renderer를 반환한다.</summary>
    internal static MeshRenderer CreatePart(string name, Transform parent, Mesh mesh, params Material[] materials)
    {
        GameObject part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        part.layer = parent.gameObject.layer;
        part.transform.SetParent(parent, false);
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    /// <summary>길이 방향으로 휘어진 칼날 메시를 반환한다. 두 번째 submesh는 밝은 날 부분이다.</summary>
    internal static Mesh CreateBlade()
    {
        const int sides = 6;
        Vector3[] vertices = new Vector3[(LENGTH_SEGMENTS + 1) * sides];
        for (int row = 0; row <= LENGTH_SEGMENTS; row++)
        {
            float t = row / (float)LENGTH_SEGMENTS;
            float tip = t < 0.86f ? 1f : Mathf.Lerp(1f, 0.008f, (t - 0.86f) / 0.14f);
            float width = Mathf.Lerp(0.052f, 0.040f, t) * tip;
            float thickness = 0.006f * tip;
            float curve = -0.075f * t * t;
            Vector3 center = new Vector3(curve, t * BLADE_LENGTH, 0f);
            vertices[row * sides] = center + new Vector3(-width * 0.5f, 0f, -thickness * 0.45f);
            vertices[row * sides + 1] = center + new Vector3(width * 0.18f, 0f, -thickness);
            vertices[row * sides + 2] = center + new Vector3(width * 0.5f, 0f, 0f);
            vertices[row * sides + 3] = center + new Vector3(width * 0.18f, 0f, thickness);
            vertices[row * sides + 4] = center + new Vector3(-width * 0.5f, 0f, thickness * 0.45f);
            vertices[row * sides + 5] = center + new Vector3(-width * 0.54f, 0f, 0f);
        }
        return BuildMesh("Shirasaya curved steel blade", vertices, LENGTH_SEGMENTS, sides, true);
    }

    /// <summary>handle이 true이면 손잡이, false이면 곡선 칼집 메시를 반환한다. 생성 메시의 해제는 호출자가 담당한다.</summary>
    internal static Mesh CreateWood(bool handle)
    {
        int rows = handle ? 8 : LENGTH_SEGMENTS;
        Vector3[] vertices = new Vector3[(rows + 1) * WOOD_SIDES];
        for (int row = 0; row <= rows; row++)
        {
            float t = row / (float)rows;
            float y = handle ? Mathf.Lerp(-0.255f, -0.014f, t) : Mathf.Lerp(-0.007f, 0.85f, t);
            float curve = handle ? 0f : -0.075f * t * t;
            float end = handle ? Mathf.Lerp(0.90f, 1f, Mathf.Sin(t * Mathf.PI * 0.5f)) : Mathf.Lerp(1f, 0.82f, t);
            if ((!handle && row == rows) || (handle && row == 0))
                end *= 0.87f;
            for (int side = 0; side < WOOD_SIDES; side++)
            {
                float angle = side * Mathf.PI * 2f / WOOD_SIDES;
                vertices[row * WOOD_SIDES + side] = new Vector3(
                    curve + Mathf.Cos(angle) * 0.034f * end,
                    y,
                    Mathf.Sin(angle) * 0.021f * end);
            }
        }
        return BuildMesh(handle ? "Shirasaya wood grip" : "Shirasaya curved saya", vertices, rows, WOOD_SIDES, false);
    }

    /// <summary>링 정점과 행·면 개수로 닫힌 메시를 반환한다. blade이면 날 부분에 두 번째 재질을 배정한다.</summary>
    private static Mesh BuildMesh(string name, Vector3[] vertices, int rows, int sides, bool blade)
    {
        List<int> body = new List<int>();
        List<int> edge = new List<int>();
        for (int row = 0; row < rows; row++)
        {
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                int a = row * sides + side;
                int b = (row + 1) * sides + side;
                int c = (row + 1) * sides + next;
                int d = row * sides + next;
                List<int> triangles = blade && (side == 1 || side == 2) ? edge : body;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(a); triangles.Add(c); triangles.Add(d);
            }
        }
        for (int side = 1; side < sides - 1; side++)
        {
            body.Add(0); body.Add(side); body.Add(side + 1);
            body.Add(rows * sides); body.Add(rows * sides + side + 1); body.Add(rows * sides + side);
        }
        Mesh mesh = new Mesh { name = name };
        mesh.vertices = vertices;
        mesh.subMeshCount = blade ? 2 : 1;
        mesh.SetTriangles(body, 0);
        if (blade)
            mesh.SetTriangles(edge, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
