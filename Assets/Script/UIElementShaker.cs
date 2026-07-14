using UnityEngine;

/// <summary>
/// [리팩토링됨]
/// UI 요소(RectTransform)에 국소적인 흔들림 효과를 줍니다.
/// Initialize() 메서드 대신, Start()에서 직접 CarController를 찾도록 수정되었습니다.
/// 충격 효과(TriggerImpactShake)는 CarCollisionHandler가 호출합니다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class UIElementShaker : MonoBehaviour
{
    [Header("엔진/주행 진동 효과")]
    public float vibrationAmount = 2.0f;
    public float vibrationSpeed = 20f;
    public float steeringVibrationFactor = 0.5f;

    [Header("시동 진동 효과")]
    public float startupVibrationBoost = 2.0f;
    public float startupVibrationDecayRate = 3.0f;

    [Header("외부 충격 효과")]
    public float impactIntensity = 15.0f;
    public float impactDecayRate = 5.0f;

    // --- Private Member Variables ---
    private CarController carController; // 차량의 데이터를 참조하기 위한 컨트롤러
    private RectTransform rectTransform;
    private Vector2 originalAnchoredPos;
    private bool wasEngineOnLastFrame = false;
    private float currentVibrationSpeed = 0f;
    private float currentImpactBoost = 0f;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        originalAnchoredPos = rectTransform.anchoredPosition;
        currentVibrationSpeed = vibrationSpeed;

        // [수정됨] CarController를 씬에서 직접 찾습니다.
        carController = FindObjectOfType<CarController>();
        if (carController == null)
        {
            Debug.LogWarning("UIElementShaker: 씬에서 CarController를 찾을 수 없습니다! 주행 진동 효과가 작동하지 않을 수 있습니다.");
            // 씬에 여러 UI Shaker가 있을 수 있으므로 비활성화하지는 않습니다.
        }
        else
        {
            wasEngineOnLastFrame = carController.IsEngineOn();
        }
    }

    void LateUpdate()
    {
        // 외부 충격 효과 (충돌 핸들러가 currentImpactBoost 값을 설정)
        Vector2 impactOffset = Vector2.zero;
        if (currentImpactBoost > 0.01f)
        {
            float impactX = (Mathf.PerlinNoise(Time.time * vibrationSpeed * 5f, 100f) - 0.5f) * currentImpactBoost;
            float impactY = (Mathf.PerlinNoise(100f, Time.time * vibrationSpeed * 5f) - 0.5f) * currentImpactBoost;
            impactOffset = new Vector2(impactX, impactY);
            currentImpactBoost = Mathf.Lerp(currentImpactBoost, 0f, Time.deltaTime * impactDecayRate);
        }

        // CarController를 찾지 못했다면 주행 진동은 생략합니다.
        if (carController == null)
        {
            rectTransform.anchoredPosition = originalAnchoredPos + impactOffset; // 충격 효과만 적용
            return;
        }

        // --- 주행 진동 효과 ---
        bool isEngineOn = carController.IsEngineOn();

        if (isEngineOn && !wasEngineOnLastFrame)
        {
            currentVibrationSpeed = vibrationSpeed * startupVibrationBoost;
        }
        else
        {
            currentVibrationSpeed = Mathf.Lerp(currentVibrationSpeed, vibrationSpeed, Time.deltaTime * startupVibrationDecayRate);
        }
        wasEngineOnLastFrame = isEngineOn;

        float intensityMultiplier = 0f;
        if (isEngineOn)
        {
            intensityMultiplier = 1.0f;
            // [수정됨] CarController의 Input 컴포넌트를 직접 참조하지 않고, GetAxis를 사용합니다.
            float steeringInputAmount = Mathf.Abs(Input.GetAxis("Horizontal"));
            intensityMultiplier += steeringInputAmount * steeringVibrationFactor;
        }

        float vibX = (Mathf.PerlinNoise(Time.time * currentVibrationSpeed, 0f) - 0.5f) * vibrationAmount * intensityMultiplier;
        float vibY = (Mathf.PerlinNoise(0f, Time.time * currentVibrationSpeed) - 0.5f) * vibrationAmount * intensityMultiplier;
        Vector2 vibrationOffset = new Vector2(vibX, vibY);

        // --- 최종 효과 적용 ---
        rectTransform.anchoredPosition = originalAnchoredPos + vibrationOffset + impactOffset;
    }

    /// <summary>
    /// 외부(CarCollisionHandler)에서 호출하여 강한 충격 효과를 발동시킵니다.
    /// </summary>
    public void TriggerImpactShake()
    {
        currentImpactBoost += impactIntensity;
    }
}
