using UnityEditor;
using UnityEngine;

public static class Window_RoundSelectedTransform
{
    private const string MenuPath = "Tools/Transform/Round Selected Transform";

    [MenuItem(MenuPath, true)]
    private static bool CanRoundSelectedTransform()
    {
        return Selection.transforms.Length > 0;
    }

    [MenuItem(MenuPath,false, UtilityMenuPriority.Transform+1)]
    private static void RoundSelectedTransform()
    {
        Transform[] transforms = Selection.transforms;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform target = transforms[i];
            Undo.RecordObject(target, "Round Selected Transform");

            // Scale은 정수로 반올림하면 0.5 → 0처럼 오브젝트가 사라지므로 소수점 2자리까지 유지한다.
            target.localPosition = Round(target.localPosition, 0);
            target.localEulerAngles = Round(target.localEulerAngles, 0);
            target.localScale = Round(target.localScale, 2);

            EditorUtility.SetDirty(target);
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
        }
    }

    private static Vector3 Round(Vector3 value, int digits)
    {
        return new Vector3(
            Round(value.x, digits),
            Round(value.y, digits),
            Round(value.z, digits)
        );
    }

    // Mathf.Round는 .5를 짝수 쪽으로 보내므로(2.5 → 2) 일반적인 반올림(AwayFromZero)을 쓴다.
    private static float Round(float value, int digits)
        => (float)System.Math.Round(value, digits, System.MidpointRounding.AwayFromZero);
}
