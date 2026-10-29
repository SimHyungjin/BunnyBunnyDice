using System.Collections.Generic;
using UnityEngine;

/// <summary>레벨업 주사위 카드의 면체를 기울인다. 면체마다 가중치에 곱할 값을 둔다(하이 롤러가 2026-10-29~30에 쓰다가 눈 가중치로 바꿨다 — 지금은 쓰는 특성이 없다).</summary>
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
