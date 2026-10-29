using System.Collections.Generic;
using UnityEngine;

/// <summary>빌더: 레벨업 주사위 카드에 높은 면체가 더 자주 나온다(2026-10-29 사용자). 면체마다 가중치에 곱할 값을 둔다.</summary>
[CreateAssetMenu(menuName = "Def/HeroTrait/Faces Bias", fileName = "SO_HeroTrait_FacesBias")]
public sealed class SO_HeroTrait_FacesBias : SO_HeroTrait, ILevelUpFaces
{
    [System.Serializable]
    public struct FacesScale
    {
        public int Faces;
        [Min(0f)] public float Scale;
    }

    [Tooltip("면체마다 SO_LevelUpConfig.FacesWeights에 곱할 값. 빠진 면체는 그대로(1).")]
    public List<FacesScale> Scales = new List<FacesScale>();

    public float ScaleFaces(int faces)
    {
        for (int i = 0; i < Scales.Count; i++)
        {
            if (Scales[i].Faces == faces) return Scales[i].Scale;
        }
        return 1f;
    }
}
