using UnityEngine;

/// <summary>
/// MainCamera에 부착되어 주행 진동과 충격 흔들림을 줍니다.
///
/// 어느 차량의 진동인지는 <see cref="SetVehicle"/>로 주입받습니다.
/// 예전에는 <c>FindObjectOfType&lt;CarController&gt;()</c>로 씬의 아무 차나 잡았고,
/// 조향 세기도 <c>Input.GetAxis</c>를 직접 읽었습니다. 그래서 차가 두 대가 되면
/// 엉뚱한 차의 상태로 떨렸고, 입력을 읽는 곳이 세 군데로 갈라져 있었습니다.
///
/// 충격 효과(TriggerImpactShake)는 CarCollisionHandler가 호출합니다.
/// </summary>
public class CarCameraEffects : MonoBehaviour
{
    // --- Public Member Variables ---

    /// <summary>주행 진동으로 흔들리는 폭입니다. (로컬 좌표 단위)</summary>
    [Header("엔진/주행 진동 효과")]
    [Tooltip("주행 진동으로 흔들리는 폭")]
    public float vibrationAmount = 0.01f;

    /// <summary>주행 진동의 기본 속도입니다. 노이즈 샘플링 주기로 쓰입니다.</summary>
    [Tooltip("주행 진동의 기본 속도")]
    public float vibrationSpeed = 20f;

    /// <summary>조향 입력이 진동 세기에 더해지는 비율입니다.</summary>
    [Tooltip("조향 입력이 진동 세기에 더해지는 비율")]
    public float steeringVibrationFactor = 0.5f;

    /// <summary>시동을 거는 순간 진동 속도에 곱해지는 배율입니다.</summary>
    [Header("시동 진동 효과")]
    [Tooltip("시동을 거는 순간 진동 속도에 곱해지는 배율")]
    public float startupVibrationBoost = 2.0f;

    /// <summary>시동 진동이 평상시 속도로 잦아드는 속도입니다.</summary>
    [Tooltip("시동 진동이 평상시 속도로 잦아드는 속도")]
    public float startupVibrationDecayRate = 3.0f;

    /// <summary>충격 한 번이 더하는 흔들림의 세기입니다.</summary>
    [Header("외부 충격 효과")]
    [Tooltip("충격 한 번이 더하는 흔들림의 세기")]
    public float impactIntensity = 0.2f;

    /// <summary>충격 흔들림이 사그라드는 속도입니다.</summary>
    [Tooltip("충격 흔들림이 사그라드는 속도")]
    public float impactDecayRate = 5.0f;

    // --- Private Member Variables ---

    private Vehicle vehicle;             // 지금 타고 있는 차량 (주입받습니다)

    /// <summary>흔들림이 없을 때 돌아갈 원래 로컬 위치입니다.</summary>
    private Vector3 originalLocalPos;

    /// <summary>직전 프레임의 시동 상태입니다. 시동이 걸리는 순간을 잡아내는 데 씁니다.</summary>
    private bool wasEngineOnLastFrame = false;

    /// <summary>지금 적용 중인 진동 속도입니다. 시동 직후 잠시 빨라졌다가 평상시 값으로 돌아옵니다.</summary>
    private float currentVibrationSpeed = 0f;

    /// <summary>남아 있는 충격 흔들림의 양입니다. 매 프레임 impactDecayRate만큼 줄어듭니다.</summary>
    private float currentImpactBoost = 0f;

    // --- Unity Event Functions ---

    /// <summary>
    /// 흔들림의 기준이 될 원래 로컬 위치와 진동 속도를 저장합니다.
    /// </summary>
    void Awake()
    {
        originalLocalPos = transform.localPosition;
        currentVibrationSpeed = vibrationSpeed;
    }

    /// <summary>
    /// 충격 흔들림과 주행 진동을 합쳐 카메라의 로컬 위치에 적용합니다.
    /// 카메라 추적이 끝난 뒤에 덮어써야 하므로 LateUpdate에서 처리합니다.
    /// </summary>
    void LateUpdate()
    {
        // 외부 충격 효과
        Vector3 impactOffset = Vector3.zero;
        if (currentImpactBoost > 0.01f)
        {
            float impactX = (Mathf.PerlinNoise(Time.time * vibrationSpeed * 5f, 100f) - 0.5f) * currentImpactBoost;
            float impactY = (Mathf.PerlinNoise(100f, Time.time * vibrationSpeed * 5f) - 0.5f) * currentImpactBoost;
            impactOffset = new Vector3(impactX, impactY, 0);
            currentImpactBoost = Mathf.Lerp(currentImpactBoost, 0f, Time.deltaTime * impactDecayRate);
        }

        // 차량을 모르면 주행 진동은 생략하고 충격만 적용합니다.
        if (vehicle == null)
        {
            transform.localPosition = originalLocalPos + impactOffset;
            return;
        }

        // --- 주행 진동 효과 ---
        bool isEngineOn = IsEngineOn();

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

            // 조향 세기는 그 차량의 입력에서 읽습니다. (입력 소스를 하나로 유지)
            float steering = vehicle.input != null ? Mathf.Abs(vehicle.input.SteerInput) : 0f;
            intensityMultiplier += steering * steeringVibrationFactor;
        }

        float vibX = (Mathf.PerlinNoise(Time.time * currentVibrationSpeed, 0f) - 0.5f) * vibrationAmount * intensityMultiplier;
        float vibY = (Mathf.PerlinNoise(0f, Time.time * currentVibrationSpeed) - 0.5f) * vibrationAmount * intensityMultiplier;
        Vector3 vibrationOffset = new Vector3(vibX, vibY, 0);

        // --- 최종 효과 적용 ---
        transform.localPosition = originalLocalPos + vibrationOffset + impactOffset;
    }

    // --- Public Methods ---

    /// <summary>
    /// 어느 차량의 진동을 표현할지 지정합니다. PlayerModeController가 탑승할 때 호출합니다.
    /// </summary>
    /// <param name="target">진동의 기준이 될 차량. null이면 주행 진동을 멈춥니다.</param>
    public void SetVehicle(Vehicle target)
    {
        vehicle = target;
        wasEngineOnLastFrame = IsEngineOn();
    }

    /// <summary>
    /// 외부(CarCollisionHandler)에서 호출하여 강한 충격 효과를 발동시킵니다.
    /// </summary>
    public void TriggerImpactShake()
    {
        currentImpactBoost += impactIntensity;
    }

    // --- Private Methods ---

    /// <summary>
    /// 지금 따라가고 있는 차량의 시동이 걸려 있는지 확인합니다.
    /// </summary>
    /// <returns>차량과 컨트롤러가 있고 시동이 걸려 있으면 true를 반환합니다.</returns>
    private bool IsEngineOn()
    {
        return vehicle != null && vehicle.controller != null && vehicle.controller.IsEngineOn();
    }
}
