using UnityEngine;

/// <summary>
/// ScriptableObject 참조 필드를 인스펙터에서 그 자리에 펼쳐 보여준다(PropertyDrawer_InlineScriptableObject).
/// 참조를 따라가 SO를 따로 선택하지 않고, 컴포넌트 하나에서 여러 SO의 값을 함께 보고 고치기 위해 쓴다.
/// List·배열 필드에 붙이면 요소마다 적용된다.
/// </summary>
public sealed class InlineScriptableObjectAttribute : PropertyAttribute { }
