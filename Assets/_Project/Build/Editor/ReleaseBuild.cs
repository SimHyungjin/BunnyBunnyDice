using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Windows 빌드 메뉴 두 개 — 출시 빌드와 개발 빌드.
/// 출시 빌드는 DEBUG_ENABLE을 잠깐 빼고 빌드한 뒤 되돌린다. 이 심볼은 PC에 늘 켜 두는데(에디터 로그가 이것으로 켜진다 — CLAUDE.md),
/// 그대로 빌드하면 출시 빌드에도 일반 로그가 남는다. 개발 빌드는 판별 기록(RunLogs)·테스트 창이 들어간다 — 빌드로 한 판을 분석할 때.
/// 빌드 씬·Addressables는 평소 빌드와 같다(Build Settings 목록, Addressables는 플레이어 빌드 때 같이 만든다).
/// </summary>
public static class ReleaseBuild
{
    private const string DebugSymbol = "DEBUG_ENABLE";
    private const string ExeName = "BunnyBunnyDice.exe";

    [MenuItem("Tools/Build/Release (Windows)", false, 1)]
    private static void BuildRelease() => Build("Builds/Release", BuildOptions.None, true);

    [MenuItem("Tools/Build/Development (Windows)", false, 2)]
    private static void BuildDevelopment() => Build("Builds/Development", BuildOptions.Development, false);

    private static void Build(string folder, BuildOptions options, bool stripDebug)
    {
        NamedBuildTarget target = NamedBuildTarget.Standalone;
        string saved = PlayerSettings.GetScriptingDefineSymbols(target);
        try
        {
            if (stripDebug) PlayerSettings.SetScriptingDefineSymbols(target, RemoveSymbol(saved, DebugSymbol));
            BuildPlayerOptions build = new BuildPlayerOptions
            {
                scenes = EnabledScenes(),
                locationPathName = Path.Combine(folder, ExeName),
                target = BuildTarget.StandaloneWindows64,
                options = options,
            };
            BuildReport report = BuildPipeline.BuildPlayer(build);
            BuildSummary summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[ReleaseBuild] {folder} 완료 — {summary.totalSize / (1024 * 1024)}MB, {summary.totalTime.TotalSeconds:0}초");
                EditorUtility.RevealInFinder(build.locationPathName);
            }
            else Debug.LogError($"[ReleaseBuild] {folder} 실패 — {summary.result}, 오류 {summary.totalErrors}개");
        }
        finally
        {
            // 실패해도 심볼은 되돌린다 — 안 되돌리면 에디터 로그가 꺼진 채로 남는다.
            if (stripDebug)
            {
                PlayerSettings.SetScriptingDefineSymbols(target, saved);
                // 빌드는 심볼을 뺀 상태로 ProjectSettings.asset을 저장하고, 되돌린 값은 메모리에만 남는다 — 바로 저장하지 않으면
                // 파일(=git)에는 DEBUG_ENABLE이 빠진 채로 남아 에디터를 다시 열 때 로그가 꺼진다(2026-11-11).
                AssetDatabase.SaveAssets();
            }
        }
    }

    private static string[] EnabledScenes()
    {
        EditorBuildSettingsScene[] all = EditorBuildSettings.scenes;
        int count = 0;
        for (int i = 0; i < all.Length; i++) if (all[i].enabled) count++;
        string[] scenes = new string[count];
        int k = 0;
        for (int i = 0; i < all.Length; i++) if (all[i].enabled) scenes[k++] = all[i].path;
        return scenes;
    }

    private static string RemoveSymbol(string symbols, string symbol)
    {
        string[] parts = symbols.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        string result = "";
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Trim() == symbol) continue;
            result += result.Length > 0 ? ";" + parts[i] : parts[i];
        }
        return result;
    }
}
