using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 칩·목표물 잭팟 칩에 쓰는 메시를 만든다: 칩 한 장(옆면 + 둥글린 윗모서리 + 윗면)과 윗면에 인쇄할 원판.
/// 실제 크기로 만든다 — 단위 원기둥을 늘리면 모서리 둥글림이 가로·세로로 따로 늘어나 납작한 칩에서 찌그러진다.
/// 밑면도 만든다 — 보이지는 않지만 툰 아웃라인(뒷면을 부풀려 그림)이 바닥에 닿는 테두리를 그리려면 아래쪽 뒷면이 있어야 한다. 피벗은 칩 밑면 가운데다.
/// </summary>
public static class ChipMesh
{
    public static Mesh CreateChip(float diameter, float height, float bevel, int segments)
    {
        RoundedRectPath circle = CreateCircle(diameter * 0.5f, segments);
        float b = Mathf.Clamp(bevel, 0f, Mathf.Min(height, diameter * 0.5f));

        // 단면: 옆면을 곧게 올라가다 사분원으로 꺾여 윗면으로 들어간다.
        List<ProfilePoint> profile = new List<ProfilePoint>();
        profile.Add(new ProfilePoint(0f, 0f, 1f, 0f));
        int arc = Mathf.Max(2, segments / 8);
        for (int i = 0; i <= arc; i++)
        {
            float t = 0.5f * Mathf.PI * i / arc;
            float c = Mathf.Cos(t), s = Mathf.Sin(t);
            profile.Add(new ProfilePoint(-b + b * c, height - b + b * s, c, s));
        }
        Mesh side = SweepMesh.Build("ChipSide", circle, profile, 1f);

        RoundedRectPath top = CreateCircle(diameter * 0.5f - b, segments);
        Mesh cap = SweepMesh.BuildCap("ChipTop", top, height, 1f);
        Mesh bottom = FlipDown(SweepMesh.BuildCap("ChipBottom", circle, 0f, 1f));
        return Combine("Chip", side, cap, bottom);
    }

    // 위를 보는 원판을 아래를 보게 뒤집는다(감는 방향과 법선을 함께).
    private static Mesh FlipDown(Mesh cap)
    {
        int[] triangles = cap.triangles;
        for (int i = 0; i < triangles.Length; i += 3) (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
        Vector3[] normals = cap.normals;
        for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.down;
        cap.triangles = triangles;
        cap.normals = normals;
        return cap;
    }

    /// <summary>위를 보는 납작한 원판(높이 0). 칩 윗면 인쇄에 쓴다.</summary>
    public static Mesh CreateDisc(float diameter, int segments) =>
        SweepMesh.BuildCap("ChipDisc", CreateCircle(diameter * 0.5f, segments), 0f, 1f);

    // 원은 반지름이 변의 절반인 둥근 정사각형이다. 모서리 네 개로 나뉘므로 나눔 수는 4로 나눈다.
    private static RoundedRectPath CreateCircle(float radius, int segments)
    {
        RoundedRectPath path = new RoundedRectPath();
        path.Build(new Vector2(-radius, -radius), new Vector2(radius, radius), radius, Mathf.Max(1, segments / 4));
        return path;
    }

    private static Mesh Combine(string name, params Mesh[] meshes)
    {
        CombineInstance[] parts = new CombineInstance[meshes.Length];
        for (int i = 0; i < meshes.Length; i++) parts[i] = new CombineInstance { mesh = meshes[i], transform = Matrix4x4.identity };
        Mesh mesh = new Mesh { name = name };
        mesh.CombineMeshes(parts, true, false);
        for (int i = 0; i < meshes.Length; i++) Object.Destroy(meshes[i]);
        return mesh;
    }
}
