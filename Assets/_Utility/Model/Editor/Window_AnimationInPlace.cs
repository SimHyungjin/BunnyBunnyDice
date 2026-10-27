using System.IO;
using UnityEditor;
using UnityEngine;

public static class Window_AnimationInPlace
{
    private const string MenuPath = "Tools/Model/AnimationInPlace";

    [MenuItem(MenuPath, true)]
    private static bool CanCreateInPlaceCopies()
    {
        Object[] selection = Selection.objects;
        for (int i = 0; i < selection.Length; i++)
        {
            // FBX 안의 클립은 복사할 수 없으므로 독립된 .anim만 허용 (FBX 클립은 Ctrl+D로 먼저 추출)
            if (selection[i] is AnimationClip && AssetDatabase.GetAssetPath(selection[i]).EndsWith(".anim"))
                return true;
        }
        return false;
    }

    [MenuItem(MenuPath,false, UtilityMenuPriority.Model)]
    private static void CreateInPlaceCopies()
    {
        int created = 0;
        Object[] selection = Selection.objects;

        for (int i = 0; i < selection.Length; i++)
        {
            if (selection[i] is not AnimationClip clip)
                continue;

            string sourcePath = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(sourcePath) || !sourcePath.EndsWith(".anim"))
            {
                UnityEngine.Debug.LogWarning($"Skip {clip.name}: only standalone .anim clips can be copied in-place.");
                continue;
            }

            string directory = Path.GetDirectoryName(sourcePath)?.Replace('\\', '/');
            string fileName = Path.GetFileNameWithoutExtension(sourcePath);
            string copyPath = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{fileName}_InPlace.anim");

            if (!AssetDatabase.CopyAsset(sourcePath, copyPath))
            {
                UnityEngine.Debug.LogWarning($"Failed to copy animation clip: {sourcePath}");
                continue;
            }

            AnimationClip copy = AssetDatabase.LoadAssetAtPath<AnimationClip>(copyPath);
            if (copy == null)
            {
                UnityEngine.Debug.LogWarning($"Failed to load copied animation clip: {copyPath}");
                continue;
            }

            int flattened = FlattenPlanarRootCurves(copy);
            Selection.activeObject = copy;
            UnityEngine.Debug.Log($"Created in-place clip: {copyPath} (flattened {flattened} root XZ curves)", copy);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        UnityEngine.Debug.Log($"Created {created} in-place animation clip copy/copies.");
    }

    private static int FlattenPlanarRootCurves(AnimationClip clip)
    {
        int count = 0;
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);

        for (int i = 0; i < bindings.Length; i++)
        {
            EditorCurveBinding binding = bindings[i];
            if (!IsPlanarRootCurve(binding.propertyName))
                continue;

            AnimationCurve source = AnimationUtility.GetEditorCurve(clip, binding);
            if (source == null)
                continue;

            Keyframe[] keys = source.keys;
            for (int k = 0; k < keys.Length; k++)
            {
                keys[k].value = 0f;
                keys[k].inTangent = 0f;
                keys[k].outTangent = 0f;
            }

            AnimationUtility.SetEditorCurve(clip, binding, new AnimationCurve(keys));
            count++;
        }

        EditorUtility.SetDirty(clip);
        return count;
    }

    private static bool IsPlanarRootCurve(string propertyName)
        => propertyName == "RootT.x"
        || propertyName == "RootT.z"
        || propertyName == "MotionT.x"
        || propertyName == "MotionT.z";
}
