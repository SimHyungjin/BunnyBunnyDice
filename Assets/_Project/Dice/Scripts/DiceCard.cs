using System;
using UnityEngine;

/// <summary>
/// 주사위 카드 한 장(면체 + 색). 눈은 놓을 때 굴려 정하므로 카드에는 없다.
/// SO가 아니라 값으로 두는 이유: 색 × 면체 조합을 하나하나 에셋으로 만들지 않고, 출현할 때 색과 면체를 따로 뽑아 만든다(기획서 13).
/// 같은 값인 카드끼리 손패에서 겹친다.
/// </summary>
[Serializable]
public struct DiceCard : IEquatable<DiceCard>
{
    [Tooltip("면 수(6·12·20). 굴림 범위이자 눈의 상한.")]
    public int Faces;
    public DiceColor Colors;

    public DiceCard(int faces, DiceColor colors)
    {
        Faces = faces;
        Colors = colors;
    }

    public bool Equals(DiceCard other) => Faces == other.Faces && Colors == other.Colors;
    public override bool Equals(object obj) => obj is DiceCard other && Equals(other);
    public override int GetHashCode() => Faces * 16 + (int)Colors;
    public static bool operator ==(DiceCard a, DiceCard b) => a.Equals(b);
    public static bool operator !=(DiceCard a, DiceCard b) => !a.Equals(b);
}
