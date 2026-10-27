using UnityEngine;

/// <summary>
/// 부채꼴 손패의 카드 자세 계산. 원 중심은 화면 아래 바깥이라 가운데 카드가 가장 높고 양끝이 처지며 기운다.
/// 장수가 늘면 각도 간격을 유지하다가 최대 각도에서 멈추고, 그 뒤로는 간격이 좁아져 겹친다.
/// 거리 설정은 손패 카드 높이 배수라 cardHeight를 곱해 캔버스 단위로 바꾼다.
/// </summary>
public static class CardHandLayout
{
    public static void GetPose(int index, int count, bool lowered, float cardHeight, SO_CardHandConfig config, out Vector2 position, out float angle)
    {
        float half = Mathf.Min(config.AnglePerCard * (count - 1) * 0.5f, config.MaxFanAngle);
        float step = count > 1 ? half * 2f / (count - 1) : 0f;
        float radius = config.FanRadius * cardHeight;
        float lower = lowered ? config.LowerOnHold * cardHeight : 0f;
        Vector2 center = new Vector2(0f, config.HandHeight * cardHeight - radius - lower);

        angle = half - step * index;
        float rad = angle * Mathf.Deg2Rad;
        position = center + new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad)) * radius;
    }
}
