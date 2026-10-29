using System;
using System.Collections.Generic;

/// <summary>
/// 판 밖 진행 세이브(기획서 12.4). 재화·캐릭터(열림·능력치 단계·최고 기록)·마지막 캐릭터·해금 진행도.
/// 값을 읽고 바꾸는 규칙은 MetaProfile이 갖는다 — 여기는 저장할 모양만 둔다.
/// </summary>
[Serializable]
public sealed class MetaSave : SaveSection
{
    // 클래스 이름이 바뀌어도 기존 세이브와 이어지도록 키를 고정한다.
    public override string Key => "Meta";

    public int Currency;

    /// <summary>캐릭터별 기록. 키 = 캐릭터 SO 에셋 이름(MetaProfile.IdOf) — 에셋 이름을 바꾸면 그 캐릭터의 기록과 끊긴다.</summary>
    public Dictionary<string, CharacterSave> Characters = new Dictionary<string, CharacterSave>();

    /// <summary>마지막에 고른 캐릭터(로비가 이 캐릭터로 열린다). 빈 값 = 고른 적 없음.</summary>
    public string LastCharacter = "";

    // ── 해금 진행도(12.4) ──
    public int BestWave;
    public int RunCount;
    public int JackpotKills;
    public int DicePlaced;
    public int Awakenings;
}

/// <summary>캐릭터 하나의 세이브.</summary>
[Serializable]
public sealed class CharacterSave
{
    public bool Unlocked;

    /// <summary>HeroStat 순서의 단계(1~6). HeroStat은 뒤에만 더한다 — 순서를 바꾸면 저장된 단계가 다른 능력치로 읽힌다.</summary>
    public int[] Stages = new int[0];

    public int BestWave;
}
