using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// 프로파일러 창에 녹화된 프레임(메인 스레드)을 글로 뽑는다 — Tools/Profiler/Export Captured Frames.
/// 프로파일러 창의 Hierarchy는 한 프레임씩만 보이고 복사할 수 없어, 여러 프레임에 걸친 평균·최대를 보거나
/// 다른 사람(AI 포함)에게 넘기기 어렵다. 녹화된 프레임을 모두 훑어 함수별 시간을 모아 텍스트 파일 하나로 남긴다.
/// 결과는 프로젝트 루트의 output/Profiler/에 쓰고 경로는 콘솔에만 남긴다(대화창은 닫을 때까지 에디터를 멈춰 자동화가 끊긴다).
/// </summary>
public static class Window_ProfilerExport
{
    private const string MenuPath = "Tools/Profiler/Export Captured Frames";
    // 위에서 아래로 펼친 나무에서 이보다 짧은(프레임당 평균 ms) 항목은 접는다 — 수천 줄이 되지 않게.
    private const float TreeMinMs = 0.1f;
    private const int TreeMaxDepth = 12;
    private const int TopSelfCount = 40;

    private sealed class Entry
    {
        public string Name;
        public int Depth;
        public double Total;
        public double Self;
        public double Calls;
        public double Gc;
        public float MaxTotal;
        public int Frames;
        public readonly List<string> Children = new List<string>();
    }

    [MenuItem(MenuPath, false, UtilityMenuPriority.Profiler)]
    private static void Export()
    {
        int first = ProfilerDriver.firstFrameIndex;
        int last = ProfilerDriver.lastFrameIndex;
        if (first < 0 || last < first)
        {
            Debug.LogWarning("[ProfilerExport] 녹화된 프레임이 없다. Window > Analysis > Profiler에서 Record를 켜고 플레이한 뒤 다시 부른다.");
            return;
        }

        // 경로(부모/자식)별로 모은다 — 같은 이름이 여러 곳에서 불려도 어디서 무거운지 갈리게.
        Dictionary<string, Entry> byPath = new Dictionary<string, Entry>();
        // 이름별 자기 시간 — 경로와 상관없이 실제로 시간을 쓴 곳.
        Dictionary<string, Entry> byName = new Dictionary<string, Entry>();
        List<string> roots = new List<string>();
        List<float> frameTimes = new List<float>();
        List<int> children = new List<int>();

        for (int frame = first; frame <= last; frame++)
        {
            using (HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(frame, 0,
                       HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false))
            {
                if (view == null || !view.valid) continue;
                frameTimes.Add(view.frameTimeMs);
                Collect(view, view.GetRootItemID(), string.Empty, 0, byPath, byName, roots, children);
            }
        }

        if (frameTimes.Count == 0)
        {
            Debug.LogWarning("[ProfilerExport] 읽을 수 있는 프레임이 없다.");
            return;
        }

        string path = Write(frameTimes, byPath, byName, roots, first, last);
        Debug.Log($"[ProfilerExport] {frameTimes.Count}프레임을 내보냈다: {path}");
        EditorUtility.RevealInFinder(path);
    }

    private static void Collect(HierarchyFrameDataView view, int parentId, string parentPath, int depth,
        Dictionary<string, Entry> byPath, Dictionary<string, Entry> byName, List<string> roots, List<int> children)
    {
        children.Clear();
        view.GetItemChildren(parentId, children);
        // 재귀가 같은 목록을 다시 쓰므로 이 단계의 자식은 따로 떠 둔다.
        int[] ids = children.ToArray();
        for (int i = 0; i < ids.Length; i++)
        {
            int id = ids[i];
            string name = view.GetItemName(id);
            string path = parentPath.Length == 0 ? name : parentPath + " / " + name;
            float total = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnTotalTime);
            float self = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnSelfTime);
            float calls = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnCalls);
            float gc = view.GetItemColumnDataAsFloat(id, HierarchyFrameDataView.columnGcMemory);

            if (!byPath.TryGetValue(path, out Entry entry))
            {
                entry = new Entry { Name = name, Depth = depth };
                byPath[path] = entry;
                if (depth == 0) roots.Add(path);
                else byPath[parentPath].Children.Add(path);
            }
            Add(entry, total, self, calls, gc);

            if (!byName.TryGetValue(name, out Entry named))
            {
                named = new Entry { Name = name };
                byName[name] = named;
            }
            Add(named, total, self, calls, gc);

            if (depth < TreeMaxDepth && view.HasItemChildren(id))
                Collect(view, id, path, depth + 1, byPath, byName, roots, children);
        }
    }

    private static void Add(Entry entry, float total, float self, float calls, float gc)
    {
        entry.Total += total;
        entry.Self += self;
        entry.Calls += calls;
        entry.Gc += gc;
        entry.MaxTotal = Mathf.Max(entry.MaxTotal, total);
        entry.Frames++;
    }

    private static string Write(List<float> frameTimes, Dictionary<string, Entry> byPath, Dictionary<string, Entry> byName,
        List<string> roots, int first, int last)
    {
        CultureInfo invariant = CultureInfo.InvariantCulture;
        int frames = frameTimes.Count;
        StringBuilder sb = new StringBuilder();

        List<float> sorted = new List<float>(frameTimes);
        sorted.Sort();
        double sum = 0;
        for (int i = 0; i < sorted.Count; i++) sum += sorted[i];
        float average = (float)(sum / frames);
        sb.AppendLine($"프로파일러 내보내기 {DateTime.Now:yyyy-MM-dd HH:mm:ss} — 메인 스레드, 프레임 {first}~{last} ({frames}개)");
        sb.AppendLine(string.Format(invariant, "프레임 시간 ms: 평균 {0:0.00} ({1:0} fps) · 중간 {2:0.00} · 95% {3:0.00} · 최대 {4:0.00}",
            average, average > 0f ? 1000f / average : 0f, Percentile(sorted, 0.5f), Percentile(sorted, 0.95f), sorted[sorted.Count - 1]));
        sb.AppendLine("값은 녹화된 프레임 전체 평균(ms/프레임). 최대는 한 프레임 최댓값, 호출·GC는 프레임당 평균.");
        sb.AppendLine();

        // 자기 시간 순 — 실제로 시간을 쓴 곳.
        List<Entry> selfList = new List<Entry>(byName.Values);
        selfList.Sort((a, b) => b.Self.CompareTo(a.Self));
        sb.AppendLine($"== 자기 시간 상위 {TopSelfCount} (이름별) ==");
        sb.AppendLine("  self ms |  total ms |  max ms | calls | GC KB | 이름");
        for (int i = 0; i < selfList.Count && i < TopSelfCount; i++)
        {
            Entry e = selfList[i];
            sb.AppendLine(string.Format(invariant, "{0,9:0.000} | {1,9:0.000} | {2,7:0.00} | {3,5:0.#} | {4,5:0.0} | {5}",
                e.Self / frames, e.Total / frames, e.MaxTotal, e.Calls / frames, e.Gc / frames / 1024.0, e.Name));
        }
        sb.AppendLine();

        // 위에서 아래로 — 어디 밑에서 불렸는지.
        sb.AppendLine(string.Format(invariant, "== 호출 나무 (프레임당 평균 {0} ms 이상만) ==", TreeMinMs));
        sb.AppendLine(" total ms |  self ms |  max ms | calls | 이름");
        roots.Sort((a, b) => byPath[b].Total.CompareTo(byPath[a].Total));
        for (int i = 0; i < roots.Count; i++) WriteTree(sb, byPath, roots[i], frames, invariant);

        string folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "output", "Profiler");
        Directory.CreateDirectory(folder);
        string file = Path.Combine(folder, $"profile_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        File.WriteAllText(file, sb.ToString(), new UTF8Encoding(true));
        return file;
    }

    private static void WriteTree(StringBuilder sb, Dictionary<string, Entry> byPath, string path, int frames, CultureInfo invariant)
    {
        Entry e = byPath[path];
        if (e.Total / frames < TreeMinMs) return;
        sb.AppendLine(string.Format(invariant, "{0,9:0.000} | {1,8:0.000} | {2,7:0.00} | {3,5:0.#} | {4}{5}",
            e.Total / frames, e.Self / frames, e.MaxTotal, e.Calls / frames, new string(' ', e.Depth * 2), e.Name));
        e.Children.Sort((a, b) => byPath[b].Total.CompareTo(byPath[a].Total));
        for (int i = 0; i < e.Children.Count; i++) WriteTree(sb, byPath, e.Children[i], frames, invariant);
    }

    private static float Percentile(List<float> sorted, float ratio)
    {
        int index = Mathf.Clamp(Mathf.CeilToInt(sorted.Count * ratio) - 1, 0, sorted.Count - 1);
        return sorted[index];
    }
}
