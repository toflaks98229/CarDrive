using UnityEngine;

/// <summary>
/// [리팩토링됨]
/// MainCamera에 부착되어 국소적인 흔들림 효과를 줍니다.
/// Initialize() 메서드 대신, Start()에서 직접 CarController를 찾도록 수정되었습니다.
/// 충격 효과(TriggerImpactShake)는 CarCollisionHandler가 호출합니다.
/// </summary>
public class CarCameraEffects : MonoBehaviour
{
    [Header("엔진/주행 진동 효과")]
    public float vibrationAmount = 0.01f;
    public float vibrationSpeed = 20f;
    public float steeringVibrationFactor = 0.5f;

    [Header("시동 진동 효과")]
    public float startupVibrationBoost = 2.0f;
    public float startupVibrationDecayRate = 3.0f;

    [Header("외부 충격 효과")]
    public float impactIntensity = 0.2f;
    public float impactDecayRate = 5.0f;

    // --- Private Member Variables ---
    private CarController carController; // 차량의 데이터를 참조하기 위한 컨트롤러
    private Vector3 originalLocalPos;
    private bool wasEngineOnLastFrame = false;
    private float currentVibrationSpeed = 0f;
    private float currentImpactBoost = 0f;

    void Start()
    {
        originalLocalPos = transform.localPosition;
        currentVibrationSpeed = vibrationSpeed;

        // [수정됨] CarController를 씬에서 직접 찾습니다.
        carController = FindObjectOfType<CarController>();
        if (carController == null)
        {
            Debug.LogError("CarCameraEffects: 씬에서 CarController를 찾을 수 없습니다! 주행 진동 효과가 작동하지 않습니다.");
            this.enabled = false;
        }
        else
        {
            wasEngineOnLastFrame = carController.IsEngineOn();
        }
    }

    void LateUpdate()
    {
        // 외부 충격 효과 (충돌 핸들러가 currentImpactBoost 값을 설정)
        Vector3 impactOffset = Vector3.zero;
        if (currentImpactBoost > 0.01f)
        {
            float impactX = (Mathf.PerlinNoise(Time.time * vibrationSpeed * 5f, 100f) - 0.5f) * currentImpactBoost;
            float impactY = (Mathf.PerlinNoise(100f, Time.time * vibrationSpeed * 5f) - 0.5f) * currentImpactBoost;
            impactOffset = new Vector3(impactX, impactY, 0);
            currentImpactBoost = Mathf.Lerp(currentImpactBoost, 0f, Time.deltaTime * impactDecayRate);
        }

        // CarController를 찾지 못했다면 주행 진동은 생략합니다.
        if (carController == null)
        {
            transform.localPosition = originalLocalPos + impactOffset; // 충격 효과만 적용
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
            // 더 나은 방법은 CarController에 SteerInput을 반환하는 public getter를 만드는 것입니다.
            float steeringInputAmount = Mathf.Abs(Input.GetAxis("Horizontal"));
            intensityMultiplier += steeringInputAmount * steeringVibrationFactor;
        }

        float vibX = (Mathf.PerlinNoise(Time.time * currentVibrationSpeed, 0f) - 0.5f) * vibrationAmount * intensityMultiplier;
        float vibY = (Mathf.PerlinNoise(0f, Time.time * currentVibrationSpeed) - 0.5f) * vibrationAmount * intensityMultiplier;
        Vector3 vibrationOffset = new Vector3(vibX, vibY, 0);

        // --- 최종 효과 적용 ---
        transform.localPosition = originalLocalPos + vibrationOffset + impactOffset;
    }

    /// <summary>
    /// 외부(CarCollisionHandler)에서 호출하여 강한 충격 효과를 발동시킵니다.
    /// </summary>
    public void TriggerImpactShake()
    {
        currentImpactBoost += impactIntensity;
    }
}
