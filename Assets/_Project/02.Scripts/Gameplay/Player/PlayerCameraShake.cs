using UnityEngine;

/// <summary>
/// 도보 상태에서 충격을 받았을 때 시야를 흔듭니다.
///
/// 이 컴포넌트는 카메라가 아니라 <b>머리 위치(Head)</b>에 붙입니다.
/// 카메라는 도보일 때만 Head의 자식이 되므로, 주행 중에는 이 흔들림이
/// 자동으로 무시되고 차량 쪽 연출(CarImpactShake)과 충돌하지 않습니다.
/// </summary>
public class PlayerCameraShake : MonoBehaviour
{
    // --- Public Member Variables ---

    /// <summary>한 번 맞았을 때 쌓이는 흔들림 양입니다.</summary>
    [Header("충격 세기")]
    [Tooltip("한 번 맞았을 때 쌓이는 흔들림 양")]
    public float impactIntensity = 0.28f;

    /// <summary>흔들림이 잦아드는 속도입니다. 클수록 빨리 가라앉습니다.</summary>
    [Tooltip("흔들림이 잦아드는 속도. 클수록 빨리 가라앉습니다.")]
    public float decayRate = 4.5f;

    /// <summary>흔들림에 쓰는 펄린 노이즈의 샘플링 속도입니다.</summary>
    [Tooltip("흔들림 노이즈의 속도")]
    public float noiseSpeed = 22f;

    /// <summary>위치가 흔들리는 최대 거리(m)입니다.</summary>
    [Header("적용 범위")]
    [Tooltip("위치가 흔들리는 최대 거리(m)")]
    public float maxPositionOffset = 0.18f;

    /// <summary>시야가 흔들리는 최대 각도(도)입니다.</summary>
    [Tooltip("시야가 흔들리는 최대 각도(도)")]
    public float maxRotationAngle = 4.0f;

    // --- Public Properties ---

    /// <summary>지금 흔들리고 있는지 여부입니다.</summary>
    public bool IsShaking { get { return currentShake > 0.001f; } }

    // --- Private Member Variables ---

    /// <summary>흔들림이 기준으로 삼는 원래 로컬 위치입니다. 앉기 등으로 눈높이가 바뀌면 갱신해야 합니다.</summary>
    private Vector3 originalLocalPosition;

    /// <summary>흔들림이 기준으로 삼는 원래 로컬 회전입니다.</summary>
    private Quaternion originalLocalRotation;

    /// <summary>남아 있는 흔들림의 양(0~1)입니다. 매 프레임 decayRate만큼 잦아듭니다.</summary>
    private float currentShake;

    /// <summary>노이즈 샘플링에 쓰는 개체별 오프셋입니다. 인스턴스마다 다른 패턴을 만듭니다.</summary>
    private float seed;

    // --- Unity Event Functions ---

    /// <summary>
    /// 흔들림의 기준이 될 원래 위치·회전을 저장하고 개체별 노이즈 시드를 뽑습니다.
    /// </summary>
    void Awake()
    {
        originalLocalPosition = transform.localPosition;
        originalLocalRotation = transform.localRotation;

        // 인스턴스마다 다른 노이즈 패턴을 쓰도록 합니다.
        seed = Random.Range(0f, 100f);
    }

    /// <summary>
    /// 남은 흔들림만큼 위치와 회전에 노이즈를 얹고 그 양을 줄입니다.
    /// 시점 이동이 끝난 뒤에 덮어써야 하므로 LateUpdate에서 처리합니다.
    /// </summary>
    void LateUpdate()
    {
        if (currentShake <= 0.001f)
        {
            // 다 잦아들었으면 원위치로 정확히 복구합니다.
            if (currentShake != 0f)
            {
                currentShake = 0f;
                transform.localPosition = originalLocalPosition;
                transform.localRotation = originalLocalRotation;
            }
            return;
        }

        float t = Time.time * noiseSpeed;

        // -1 ~ 1 범위의 부드러운 노이즈 3축
        float nx = Mathf.PerlinNoise(t, seed) * 2f - 1f;
        float ny = Mathf.PerlinNoise(seed, t) * 2f - 1f;
        float nz = Mathf.PerlinNoise(t, t + seed) * 2f - 1f;

        Vector3 offset = new Vector3(nx, ny, 0f) * (maxPositionOffset * currentShake);
        Vector3 angles = new Vector3(ny, nx, nz) * (maxRotationAngle * currentShake);

        transform.localPosition = originalLocalPosition + offset;
        transform.localRotation = originalLocalRotation * Quaternion.Euler(angles);

        currentShake = Mathf.Lerp(currentShake, 0f, Time.deltaTime * decayRate);
    }

    // --- Public Methods ---

    /// <summary>
    /// 시야를 흔듭니다.
    /// </summary>
    /// <param name="scale">세기 배율</param>
    public void TriggerImpactShake(float scale)
    {
        currentShake = Mathf.Clamp01(currentShake + impactIntensity * scale);
    }

    /// <summary>기본 세기로 흔듭니다.</summary>
    public void TriggerImpactShake()
    {
        TriggerImpactShake(1f);
    }

    /// <summary>
    /// 흔들림이 기준으로 삼는 위치입니다.
    /// 앉기처럼 눈높이가 바뀔 때 이 값을 갱신해 주어야 흔들림이 원래 높이로 되돌리지 않습니다.
    /// </summary>
    public Vector3 RestLocalPosition
    {
        get { return originalLocalPosition; }
        set { originalLocalPosition = value; }
    }

    /// <summary>흔들림을 즉시 멈추고 원위치로 되돌립니다.</summary>
    public void StopShake()
    {
        currentShake = 0f;
        transform.localPosition = originalLocalPosition;
        transform.localRotation = originalLocalRotation;
    }
}
