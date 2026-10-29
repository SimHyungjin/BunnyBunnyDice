using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>단면 위의 점 하나. d = 경로에서 바깥으로 민 거리, h = 높이. 법선도 단면 평면 안의 방향(바깥, 위)으로 준다.</summary>
public struct ProfilePoint
{
    public float D, H, NormalD, NormalH;
    // 앞 점과 잇지 않는다 — 각진 모서리는 같은 자리에 법선만 다른 점을 두 번 넣고 두 번째에 이 값을 켠다.
    public bool Break;

    public ProfilePoint(float d, float h, float normalD, float normalH, bool breakBefore = false)
    {
        D = d;
        H = h;
        NormalD = normalD;
        NormalH = normalH;
        Break = breakBefore;
    }
}

/// <summary>
/// 단면(ProfilePoint 목록)을 닫힌 경로(RoundedRectPath)를 따라 돌려 메시를 만든다. 칩 옆면이 이것이다.
/// 법선은 단면이 정한 값을 그대로 쓴다 — RecalculateNormals는 각진 모서리까지 뭉개 면 경계가 흐려진다.
/// 면 방향은 삼각형마다 법선과 비교해 맞춘다(단면을 어느 쪽으로 그리든 앞면이 바깥을 보게).
/// </summary>
public static class SweepMesh
{
    /// <summary>닫힌 경로를 따라 돌린다(끝이 처음과 이어진다).</summary>
    public static Mesh Build(string name, RoundedRectPath path, List<ProfilePoint> profile, float uvScale)
    {
        List<Vector3> pathPoints = path.Points;
        List<Vector3> pathNormals = path.Normals;
        int rings = pathPoints.Count;
        int columns = profile.Count;
        Vector3[] vertices = new Vector3[rings * columns];
        Vector3[] normals = new Vector3[rings * columns];
        Vector2[] uvs = new Vector2[rings * columns];

        for (int i = 0; i < rings; i++)
        {
            Vector3 p = pathPoints[i];
            Vector3 n = pathNormals[i];
            for (int j = 0; j < columns; j++)
            {
                ProfilePoint q = profile[j];
                int index = i * columns + j;
                vertices[index] = p + n * q.D + Vector3.up * q.H;
                normals[index] = (n * q.NormalD + Vector3.up * q.NormalH).normalized;
                // 질감 없는 단색 재질이 대부분이라 UV는 위에서 본 좌표로 충분하다(인쇄 선·펠트와 결이 맞는다).
                uvs[index] = new Vector2(vertices[index].x, vertices[index].z) * uvScale;
            }
        }

        List<int> triangles = new List<int>(rings * columns * 6);
        for (int i = 0; i < rings; i++)
        {
            int next = (i + 1) % rings;
            for (int j = 0; j + 1 < columns; j++)
            {
                if (profile[j + 1].Break) continue;
                int a = i * columns + j, b = next * columns + j, c = next * columns + j + 1, d = i * columns + j + 1;
                AddTriangle(triangles, vertices, normals, a, b, c);
                AddTriangle(triangles, vertices, normals, a, c, d);
            }
        }

        return CreateMesh(name, vertices, normals, uvs, triangles);
    }

    /// <summary>경로 안쪽을 평평하게 채운다(펠트 윗면). 둥근 사각형은 볼록하므로 가운데에서 부채꼴로 나누면 된다.</summary>
    public static Mesh BuildCap(string name, RoundedRectPath path, float height, float uvScale)
    {
        int count = path.Count;
        Vector3 center = Vector3.zero;
        for (int i = 0; i < count; i++) center += path.Points[i];
        center /= count;

        Vector3[] vertices = new Vector3[count + 1];
        Vector3[] normals = new Vector3[count + 1];
        Vector2[] uvs = new Vector2[count + 1];
        for (int i = 0; i <= count; i++)
        {
            Vector3 p = i < count ? path.Points[i] : center;
            vertices[i] = new Vector3(p.x, height, p.z);
            normals[i] = Vector3.up;
            uvs[i] = new Vector2(p.x, p.z) * uvScale;
        }

        List<int> triangles = new List<int>(count * 3);
        for (int i = 0; i < count; i++) AddTriangle(triangles, vertices, normals, count, i, (i + 1) % count);
        return CreateMesh(name, vertices, normals, uvs, triangles);
    }

    // 삼각형이 법선 쪽을 보도록 감는 방향을 맞춘다. 넓이가 0인 삼각형(같은 자리의 점)은 넣지 않는다.
    private static void AddTriangle(List<int> triangles, Vector3[] vertices, Vector3[] normals, int a, int b, int c)
    {
        Vector3 face = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
        if (face.sqrMagnitude < 1e-12f) return;
        Vector3 normal = normals[a] + normals[b] + normals[c];
        triangles.Add(a);
        if (Vector3.Dot(face, normal) >= 0f)
        {
            triangles.Add(b);
            triangles.Add(c);
        }
        else
        {
            triangles.Add(c);
            triangles.Add(b);
        }
    }

    private static Mesh CreateMesh(string name, Vector3[] vertices, Vector3[] normals, Vector2[] uvs, List<int> triangles)
    {
        Mesh mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
