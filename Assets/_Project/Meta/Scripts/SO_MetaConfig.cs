using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 해금 조건 종류(기획서 12.4). 진행도는 세이브의 해금 진행도에서 읽는다(MetaProfile.GetProgress).
/// 최고 웨이브 말고는 모두 누적이다 — "한 판에"·"누적" 같은 말 없이 짧게 적을 수 있고, 판을 거듭하면 언젠가 열린다.
/// </summary>
public enum UnlockKind
{
    /// <summary>최고 클리어 웨이브(모든 캐릭터 통틀어).</summary>
    BestWave,
    /// <summary>주사위 각성 누적 횟수.</summary>
    Awakenings,
    /// <summary>잭팟 몬스터 누적 처치.</summary>
    JackpotKills,
    /// <summary>누적 판 수.</summary>
    RunCount,
    /// <summary>주사위 누적 배치(빈 칸에 새로 놓은 것).</summary>
    DicePlaced,
}

/// <summary>캐릭터 하나의 해금 조건.</summary>
[Serializable]
public struct UnlockRule
{
    public SO_HeroCharacter Character;
    public UnlockKind Kind;
    [Min(1)] public int Target;
}

/// <summary>
/// 판 밖 진행 설정(기획서 12.4·12.5). 숫자는 잠정이고 나중에 시뮬레이션으로 맞춘다(11.2).
/// </summary>
[CreateAssetMenu(menuName = "Def/Meta/Meta Config", fileName = nameof(SO_MetaConfig))]
public sealed class SO_MetaConfig : SO_Config
{
    [Tooltip("잠긴 캐릭터와 해금 조건. 여기 없는 캐릭터는 처음부터 열려 있다(새 캐릭터를 만들면 바로 시험할 수 있게).\n" +
             "캐릭터는 에셋 이름으로 맞춘다 — 실행 중 복사본(SO_HeroConfig 아래)과 이 목록의 원본이 서로 다른 오브젝트라서다.")]
    public List<UnlockRule> Unlocks = new List<UnlockRule>();

    [Header("재화 · 리롤")]
    [Tooltip("판 보상 = 클리어한 웨이브 × (보상 주사위 눈의 합)(기획서 12.5). 주사위 면 수 — 로비 능력치 주사위와 같은 6면을 지킨다.")]
    [Min(1)] public int RewardDieFaces = 6;
    [Tooltip("클리어한 웨이브 이만큼마다 보상 주사위가 하나 는다(10이면 9웨이브까지 0개 = 보상 0). "
        + "웨이브가 갈수록 길어져 판 시간은 웨이브의 제곱쯤으로 느는데, 웨이브 × 주사위 수도 제곱으로 늘어 분당 재화가 판 길이와 상관없이 고르다(2026-10-28).")]
    [Min(1)] public int RewardWavesPerDie = 10;
    [Tooltip("능력치 하나를 다시 굴리는 재화.")]
    [Min(0)] public int RerollCost = 500;

    public bool TryGetRule(SO_HeroCharacter character, out UnlockRule rule)
    {
        if (character != null)
        {
            string id = MetaProfile.IdOf(character);
            for (int i = 0; i < Unlocks.Count; i++)
            {
                if (Unlocks[i].Character == null || MetaProfile.IdOf(Unlocks[i].Character) != id) continue;
                rule = Unlocks[i];
                return true;
            }
        }
        rule = default;
        return false;
    }
}
