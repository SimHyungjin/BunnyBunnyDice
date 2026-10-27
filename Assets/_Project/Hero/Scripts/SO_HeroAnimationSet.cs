using UnityEngine;

/// <summary>
/// 영웅 동작 묶음(Humanoid 클립). 모델이 Humanoid 아바타를 가지면 어느 캐릭터든 같은 묶음을 쓸 수 있어 캐릭터가 참조만 한다.
/// 재생은 HeroAnimation이 하고, 어느 동작인지는 판정·연출 코드가 정한다(애니메이션이 위치를 옮기지 않는다 — 루트 모션은 버린다).
/// 클립의 Loop 설정과 상관없이 서기·달리기·앉기는 되풀이한다(외부 팩의 import 설정을 고치지 않으려고).
/// </summary>
[CreateAssetMenu(menuName = "Def/Hero/Hero Animation Set", fileName = "SO_HeroAnimationSet_")]
public sealed class SO_HeroAnimationSet : SO_Config
{
    [Header("클립 (Humanoid)")]
    public AnimationClip Idle;
    [Tooltip("제자리 달리기(In Place). 이동 속도에 맞춰 빠르기를 바꾼다.")]
    public AnimationClip Run;
    [Tooltip("제자리 왼쪽 옆걸음 달리기. 공격 중 몸이 적을 향해 가는 방향과 어긋나면 앞 달리기와 섞는다. 없으면 앞 달리기만.")]
    public AnimationClip RunLeft;
    [Tooltip("제자리 오른쪽 옆걸음 달리기.")]
    public AnimationClip RunRight;
    [Tooltip("로비에서 난간에 걸터앉은 자세.")]
    public AnimationClip Sit;
    [Tooltip("제자리 점프(웅크림 → 공중 → 착지가 한 클립). 높이는 코드가 옮기므로 제자리여야 한다. 시각은 연출이 직접 정한다.")]
    public AnimationClip Jump;

    [Tooltip("기본 공격(상체만, 제자리). 다리는 서기·달리기를 그대로 한다 — 걸으면서 던진다. 없으면 공격 동작 없이 판정만 한다.")]
    public AnimationClip Attack;

    [Header("공격")]
    [Tooltip("카드가 손을 떠나는 시점(클립 진행도 0~1). 공격 판정 시각에 이 프레임이 오도록 동작을 앞당겨 시작한다.")]
    [Range(0f, 1f)] public float AttackRelease = 0.38f;
    [Tooltip("공격 재생 배율 상한. 공격 간격이 클립보다 짧으면 클립이 간격에 들어가게 빨리 돌리되 이 배율까지만 — 더 빠르면 동작이 떨려 보인다.")]
    [Min(1f)] public float AttackMaxRate = 2f;
    [Tooltip("공격 층을 켜고 끄는 시간(초). 짧아야 공격이 굼떠 보이지 않는다.")]
    [Min(0.01f)] public float AttackFade = 0.08f;

    [Tooltip("카드를 던지는 손. 카드가 이 손에서 날아간다.")]
    public HumanBodyBones AttackHand = HumanBodyBones.RightHand;

    [Header("조준 (공격 중 상체 비틀기)")]
    [Tooltip("카드가 손을 떠나는 순간 손이 가리키는 방향(몸 정면 기준 도, + = 오른쪽). 클립마다 다르다 — 이 방향이 적을 향하게 몸을 돌린다.")]
    [Range(-180f, 180f)] public float AttackAimYaw = 0f;
    [Tooltip("상체(허리·가슴·목)를 비트는 최대 각도(도). 더 필요하면 몸 전체를 돌린다.")]
    [Range(0f, 120f)] public float AimMaxTwist = 70f;
    [Tooltip("걷는 중 몸 전체를 가는 방향에서 돌릴 수 있는 최대 각도(도). 다리는 옆걸음 달리기로 섞인다 — 뒤로 가는 클립이 없어 90이 끝이다.")]
    [Range(0f, 90f)] public float AimMaxBodyOffset = 90f;
    [Tooltip("비틀기를 허리·가슴·목에 나누는 몫(합이 1이면 상체가 다 돌아간다). 한 뼈에 몰면 허리가 꺾여 보인다.")]
    public Vector3 AimTwistShares = new Vector3(0.4f, 0.4f, 0.2f);
    [Tooltip("비트는 각도가 따라가는 빠르기(도/초). 대상이 바뀔 때 상체가 튀지 않게.")]
    [Min(1f)] public float AimTurnSpeed = 900f;

    [Header("섞기")]
    [Tooltip("동작이 바뀔 때 섞는 시간(초).")]
    [Min(0f)] public float FadeDuration = 0.2f;
    [Tooltip("달리기 클립이 제 빠르기(1배)로 보이는 이동 속도(초당 칸). 실제 속도 / 이 값 = 재생 배율.")]
    [Min(0.01f)] public float RunReferenceSpeed = 3f;
    [Tooltip("옆걸음 클립 재생 배율(앞 달리기 대비 따로). 옆걸음 클립이 같은 이동 속도에서 발이 느리게 보일 때 올린다.")]
    [Min(0.1f)] public float RunSideRate = 1.5f;
        [Tooltip("이보다 느리면 서기로 본다(초당 칸).")]
    [Min(0f)] public float MoveThreshold = 0.05f;
    [Tooltip("달리기 재생 배율 범위. 아주 느리거나 빠를 때 발이 헛돌거나 떨지 않게 묶는다.")]
    public Vector2 RunSpeedRange = new Vector2(0.6f, 1.8f);

    [Header("점프 클립 구간 (0~1)")]
    [Tooltip("발이 땅을 떠나는 시점. 앞은 웅크림, 연출의 일어서기·웅크림 시간에 맞춰 재생한다.")]
    [Range(0f, 1f)] public float JumpTakeoff = 0.3f;
    [Tooltip("발이 땅에 닿는 시점. 사이는 공중, 연출의 나는 시간에 맞춰 재생하고 뒤는 착지 시간에 맞춘다.")]
    [Range(0f, 1f)] public float JumpLand = 0.7f;

    /// <summary>
    /// 공격 간격이 interval일 때 공격 클립 재생 배율. 간격 안에 클립이 다 들어가게 빨리 돌리되 상한까지만.
    /// 느리게는 돌리지 않는다(공격이 굼떠 보이지 않게 — 남는 시간은 서 있다).
    /// </summary>
    public float AttackRate(float interval)
    {
        if (Attack == null) return 1f;
        return interval > 0f ? Mathf.Clamp(Attack.length / interval, 1f, AttackMaxRate) : AttackMaxRate;
    }

    /// <summary>
    /// 동작 시작부터 카드가 손을 떠나기까지(초). 공격 판정(HeroAttack)이 이만큼 기다렸다 맞힌다. 공격 동작이 없으면 0(바로 맞힌다).
    /// 판정 쪽이 동작 재생과 상관없이 셀 수 있게 여기(데이터)서 계산한다 — 화면 없이 도는 AI 시뮬도 같은 시각에 맞힌다.
    /// </summary>
    public float AttackWindup(float interval) => Attack != null ? AttackRelease * Attack.length / AttackRate(interval) : 0f;
}
