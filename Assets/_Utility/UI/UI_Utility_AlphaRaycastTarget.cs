using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Image의 투명 영역이 레이캐스트에 걸리지 않도록 alphaHitTestMinimumThreshold를 설정한다.
/// 텍스처에 Read/Write가 꺼져 있으면 알파 판정이 동작하지 않으므로 경고를 출력한다.
/// Read/Write는 텍스처 사본을 CPU 메모리에 남기므로, 큰 텍스처·아틀라스에는 Sprite의 Custom Physics Shape 사용을 고려할 것.
/// </summary>
[RequireComponent(typeof(Image))]
public class UI_Utility_AlphaRaycastTarget : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] private float threshold = 0.1f;

    private void OnEnable() => Apply();

    /// <summary>런타임에 Image의 sprite를 교체했다면 호출해서 Read/Write 여부를 다시 확인한다.</summary>
    public void Apply()
    {
        var image = GetComponent<Image>();
        image.alphaHitTestMinimumThreshold = threshold;

        if (threshold <= 0f) return;

        var texture = image.sprite != null ? image.sprite.texture : null;
        if (texture != null && !texture.isReadable)
        {
            Debug.LogWarning(
                $"[{nameof(UI_Utility_AlphaRaycastTarget)}] '{name}' : " +
                "Sprite texture Read/Write Enabled가 꺼져 있어 alpha hit test가 동작하지 않습니다. " +
                "Sprite Import Settings에서 Read/Write Enabled를 켜주세요.",
                this);
        }
    }
}
