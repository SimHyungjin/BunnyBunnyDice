using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 바닥에 눕힌 납작한 고리 메시(폭발 고리·사거리 고리 공용). XZ 평면(y = 0)에 만들고, 오브젝트 위치로 중심을 옮긴다.
/// LineRenderer를 쓰지 않는 이유: 닫힌 고리를 그리면 한쪽(화면 왼쪽)에 틈·꺾임이 생겼다 — loop를 끄고 이음매를 옮겨도 그대로였다.
/// 메시는 마지막 조각이 첫 정점을 그대로 다시 쓰므로 이음매가 생길 수 없고, 굵기도 둘레 어디서나 같다.
/// 색은 정점 색으로 넣는다 — 선 머티리얼(Particles)이 정점 색을 곱하므로 LineRenderer의 start/endColor와 같은 역할이다.
/// </summary>
public static class RingMesh
{
    // 매 프레임 다시 채우므로(퍼지는 폭발) 버퍼를 재사용한다.
    private static readonly List<Vector3> Vertices = new List<Vector3>();
    private static readonly List<Color> Colors = new List<Color>();
    private static readonly List<int> Triangles = new List<int>();

    public static Mesh Create(string name)
    {
        Mesh mesh = new Mesh { name = name };
        mesh.MarkDynamic();
        return mesh;
    }

    /// <param name="radius">선 가운데까지의 반지름.</param>
    /// <param name="width">선 굵기(월드 단위). 안쪽 반지름은 0 아래로 내려가지 않는다.</param>
    public static void Fill(Mesh mesh, float radius, float width, int segments, Color color)
    {
        segments = Mathf.Max(segments, 3);
        float inner = Mathf.Max(radius - width * 0.5f, 0f);
        float outer = radius + width * 0.5f;

        Vertices.Clear();
        Colors.Clear();
        Triangles.Clear();
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vertices.Add(direction * inner);
            Vertices.Add(direction * outer);
            Colors.Add(color);
            Colors.Add(color);

            // 다음 조각의 정점 — 마지막 조각은 첫 정점으로 돌아온다.
            int a = i * 2;
            int b = (i + 1) % segments * 2;
            Triangles.Add(a);
            Triangles.Add(b);
            Triangles.Add(a + 1);
            Triangles.Add(a + 1);
            Triangles.Add(b);
            Triangles.Add(b + 1);
        }

        mesh.Clear();
        mesh.SetVertices(Vertices);
        mesh.SetColors(Colors);
        mesh.SetTriangles(Triangles, 0);
        mesh.RecalculateBounds();
    }
}
