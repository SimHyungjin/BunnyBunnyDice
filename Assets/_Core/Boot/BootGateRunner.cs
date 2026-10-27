using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

/// <summary>
/// 부팅 게이트들을 모아 두고, 시점(Phase)별로 순서대로 실행한다.
/// 하나라도 <see cref="BootGateResult.Blocked"/>를 돌려주면 그 자리에서 멈춘다.
/// </summary>
public sealed class BootGateRunner
{
    private readonly List<IBootGate> _gates = new();

    /// <summary>게이트를 등록한다. 반환값으로 체이닝할 수 있다.</summary>
    public BootGateRunner Register(IBootGate gate)
    {
        if (gate != null) _gates.Add(gate);
        return this;
    }

    /// <summary>
    /// 해당 시점의 게이트를 Order 순으로 실행한다.
    /// 통과하면 true, 어느 게이트든 부팅을 막았으면 false.
    /// </summary>
    public async UniTask<bool> RunPhaseAsync(BootGatePhase phase)
    {
        var targets = new List<IBootGate>();
        foreach (var gate in _gates)
            if (gate.Phase == phase)
                targets.Add(gate);

        if (targets.Count == 0) return true;

        // List.Sort는 안정 정렬이 아니라 Order가 같은 게이트의 순서가 실행마다 달라질 수 있다.
        // 게이트는 몇 개뿐이라 삽입 정렬로 충분하고, 삽입 정렬은 Order가 같으면 등록 순서를 지킨다.
        for (int i = 1; i < targets.Count; i++)
        {
            IBootGate gate = targets[i];
            int j = i - 1;
            while (j >= 0 && targets[j].Order > gate.Order)
            {
                targets[j + 1] = targets[j];
                j--;
            }
            targets[j + 1] = gate;
        }

        // 게이트는 순차 실행한다. 앞 게이트의 결과가 뒤 게이트의 전제가 되기 때문이다.
        // (동의 결과가 있어야 원격 설정을 읽고, 원격 설정이 있어야 버전을 판정한다.)
        foreach (var gate in targets)
        {
            BootGateResult result;

            try
            {
                result = await gate.RunAsync();
            }
            catch (Exception e)
            {
                // 게이트 예외를 부팅 전체의 예외 처리에 맡기면 "게임에 들여보내기"로 흘러간다.
                // 매니저 초기화라면 그것이 맞지만 게이트는 목적이 정반대라, 각자의 정책을 따른다.
                result = gate.FallbackOnError;
                Debug.LogError(
                    $"[BootGate] {gate.GetType().Name} 실행 중 예외가 발생해 {result}로 처리합니다.\n{e}");
            }

            if (result == BootGateResult.Blocked)
            {
                Debug.LogWarning($"[BootGate] {gate.GetType().Name}에서 부팅이 중단되었습니다.");
                return false;
            }
        }

        return true;
    }
}
