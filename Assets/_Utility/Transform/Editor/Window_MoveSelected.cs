using UnityEditor;
using UnityEngine;

public class Window_MoveSelected : EditorWindow
{
    private Vector3 offset;

    [MenuItem("Tools/Transform/Move Selected", false, UtilityMenuPriority.Transform)]
    private static void Open()
    {
        GetWindow<Window_MoveSelected>("Move Selected");
    }

    private void OnSelectionChange() => Repaint();

    private void OnGUI()
    {
        GUILayout.Label("Move Selected Objects", EditorStyles.boldLabel);

        offset = EditorGUILayout.Vector3Field("Offset", offset);

        GUILayout.Space(10);

        // Selection.transforms는 씬 오브젝트만, 부모·자식이 함께 선택되면 최상위만 반환한다.
        // (gameObjects를 쓰면 프리팹 에셋이 수정되고 자식이 두 번 이동한다)
        Transform[] targets = Selection.transforms;

        GUI.enabled = targets.Length > 0;
        if (GUILayout.Button($"Move ({targets.Length})"))
        {
            Undo.RecordObjects(targets, "Move Objects");
            foreach (Transform t in targets)
                t.position += offset;
        }
        GUI.enabled = true;
    }
}
