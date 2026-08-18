using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 체력을 Image 게이지로 표시합니다.
/// 상태는 Health(PlayerHealth / VehicleHealth)가 소유하고 이 클래스는 읽어서 그리기만 합니다.
/// (같은 Health를 TextHealthBar와 동시에 표시해도 됩니다)
/// </summary>
public class HealthBarImage : MonoBehaviour
{
    // --- Public Member Variables ---

    /// <summary>
    /// 체력을 읽어올 대상입니다. 반드시 인스펙터에서 직접 연결하세요.
    /// 자동 탐색을 두지 않는 이유는, 플레이어 체력 자리에 차량 내구도가 잡히는 사고를 막기 위해서입니다.
    /// </summary>
    [Header("연동")]
    [Tooltip("체력을 읽어올 대상. 반드시 직접 연결하세요. " +
             "예전에는 비어 있으면 씬에서 아무 체력바나 찾았는데, 그러면 " +
             "플레이어 체력을 보여줘야 할 자리에 차량 내구도가 뜰 수 있었습니다.")]
    public Health source;

    /// <summary>체력 비율을 채움으로 표현할 Image입니다. Image Type을 Filled / Horizontal로 설정하세요.</summary>
    [Header("UI")]
    [Tooltip("Image Type을 Filled / Horizontal로 설정한 게이지")]
    public Image fillImage;

    /// <summary>수치를 "현재 / 최대" 형태로 표시할 텍스트입니다. 비워 두어도 됩니다.</summary>
    [Tooltip("수치를 표시할 텍스트 (선택)")]
    public TextMeshProUGUI valueText;

    /// <summary>평상시 게이지 색상입니다.</summary>
    [Header("색상")]
    public Color fullColor = new Color(0.65f, 0.20f, 0.18f);

    /// <summary>경고 구간에서 쓰는 게이지 색상입니다.</summary>
    public Color lowColor = new Color(0.90f, 0.25f, 0.20f);

    /// <summary>이 비율 아래로 내려가면 경고 색으로 바뀌고 깜빡입니다.</summary>
    [Tooltip("이 비율 아래로 내려가면 경고 색으로 바뀌고 깜빡입니다.")]
    public float lowThreshold = 0.3f;

    /// <summary>경고 깜빡임 속도입니다. (초당 횟수)</summary>
    [Tooltip("경고 깜빡임 속도 (초당 횟수)")]
    public float blinkSpeed = 2.5f;

    /// <summary>게이지가 목표치를 따라가는 속도입니다. 0이면 즉시 반영합니다.</summary>
    [Header("연출")]
    [Tooltip("게이지가 목표치로 따라가는 속도. 0이면 즉시 반영합니다.")]
    public float smoothSpeed = 8f;

    // --- Private Member Variables ---

    /// <summary>
    /// 지금 화면에 그려지고 있는 채움 비율입니다.
    /// 실제 체력을 향해 smoothSpeed로 따라가므로 값이 급변해도 게이지는 부드럽게 움직입니다.
    /// </summary>
    private float displayed = 1f;

    // --- Unity Event Functions ---

    /// <summary>
    /// 표시 대상을 확인하고 게이지의 시작 값을 현재 체력에 맞춥니다.
    /// 대상이 없으면 경고를 남기고 이 컴포넌트를 끕니다.
    /// </summary>
    void Start()
    {
        if (source == null)
        {
            Debug.LogWarning("HealthBarImage: 표시할 Health가 연결되지 않았습니다.", this);
            enabled = false;
            return;
        }

        displayed = source.HealthNormalized;
    }

    /// <summary>
    /// 매 프레임 체력을 읽어 게이지의 채움·색상과 수치 텍스트를 갱신합니다.
    /// </summary>
    void Update()
    {
        if (source == null) return;

        float target = Mathf.Clamp01(source.HealthNormalized);

        displayed = smoothSpeed > 0f
            ? Mathf.MoveTowards(displayed, target, smoothSpeed * Time.deltaTime)
            : target;

        bool isLow = target <= lowThreshold;

        if (fillImage != null)
        {
            fillImage.fillAmount = displayed;

            Color color = isLow ? lowColor : fullColor;
            if (isLow)
            {
                // 위험 구간에서는 밝기를 흔들어 눈에 띄게 합니다.
                float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * blinkSpeed * Mathf.PI * 2f);
                color *= pulse;
                color.a = 1f;
            }
            fillImage.color = color;
        }

        if (valueText != null)
        {
            valueText.text = Mathf.CeilToInt(target * source.maxHealth) + " / " + Mathf.RoundToInt(source.maxHealth);
        }
    }
}
