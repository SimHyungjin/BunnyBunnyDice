using System;
using System.Collections.Generic;

/// <summary>
/// 본 팁 기록. 판 밖 진행(MetaSave)과 파일을 나눈다 — "팁 다시 보기"가 이 파일만 비우면 되게.
/// 팁 이름(TutorialTipId)을 문자열로 남긴다(enum 순서를 바꿔도 기록이 어긋나지 않게).
/// </summary>
[Serializable]
public sealed class TutorialSave : SaveSection
{
    public override string Key => "Tutorial";

    public List<string> Seen = new List<string>();

    public bool HasSeen(TutorialTipId id) => Seen.Contains(id.ToString());

    public void MarkSeen(TutorialTipId id)
    {
        if (HasSeen(id)) return;
        Seen.Add(id.ToString());
        MarkDirty();
    }

    public void ResetSeen()
    {
        if (Seen.Count == 0) return;
        Seen.Clear();
        MarkDirty();
    }
}
