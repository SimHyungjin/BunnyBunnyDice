using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 실행 중 설정 조정 창구. 플레이 중 하이어라키에서 이 오브젝트를 선택하면 모든 설정(복사본)이 펼쳐져 보인다.
/// - 여기서 고친 값은 이번 플레이에서만 유효하고 원본 SO에는 쓰지 않는다.
/// - 원본 SO 에셋을 고치면 영구 저장되고, 복사본도 원본 값으로 다시 덮인다(ConfigTuning).
/// - 설정마다 원본 SO 에셋으로 바로 가는 창구가 있다(인스펙터의 "원본 열기").
/// </summary>
public sealed class GameTuning : MonoBehaviour
{
    [SerializeField, InlineScriptableObject] private List<SO_Config> _configs = new List<SO_Config>();

    private ConfigTuning _tuning;

    public void Show(ConfigTuning tuning)
    {
        _tuning = tuning;
        _configs.Clear();
        for (int i = 0; i < tuning.Copies.Count; i++) _configs.Add(tuning.Copies[i]);
    }

    /// <summary>복사본의 원본 SO 에셋(인스펙터의 원본 열기).</summary>
    public SO_Config GetOriginal(SO_Config copy) => _tuning?.GetOriginal(copy);
}
