using UnityEngine;
using UnityEngine.UI; // UI 요소를 사용할 경우를 대비해 추가 (현재는 Transform만 사용)

/// <summary>
/// CarController의 데이터를 받아와 속도, RPM, 연료 계기판의 바늘(Transform)을 회전시키는 클래스입니다.
/// </summary>
public class CarUIController : MonoBehaviour
{
    // --- Public Member Variables ---

    [Header("핵심 컴포넌트 연결")]
    [Tooltip("데이터를 가져올 CarController 스크립트")]
    public CarController carController;

    [Header("계기판 바늘 (Transform)")]
    [Tooltip("속도계 바늘의 Transform")]
    public Transform speedometerNeedle;

    [Tooltip("RPM 게이지 바늘의 Transform")]
    public Transform rpmNeedle;

    [Tooltip("연료 게이지 바늘의 Transform")]
    public Transform fuelNeedle;

    [Header("계기판 최대값 설정")]
    [Tooltip("속도계에 표시될 최대 속도(km/h). 이 값을 넘어도 바늘은 최대 각도에 머무릅니다.")]
    public float maxSpeed = 240f;

    [Tooltip("RPM 게이지에 표시될 최대 RPM. 이 값을 넘어도 바늘은 최대 각도에 머무릅니다.")]
    public float maxRpm = 6000f;
    // (연료는 CarController의 maxFuel 값을 최대값으로 사용합니다)

    [Header("바늘 회전 각도 설정")]
    [Tooltip("값이 0일 때의 바늘 Z축 로컬 회전 각도")]
    public float zeroAngle = 0f;

    [Tooltip("값이 최대일 때의 바늘 Z축 로컬 회전 각도")]
    public float maxAngle = -100f; // 예: 시계 반대 방향으로 100도 회전

    // --- Unity Event Functions ---

    /// <summary>
    /// 매 프레임마다 호출됩니다.
    /// </summary>
    void Update()
    {
        // carController가 할당되지 않았다면 오류 메시지를 출력하고 실행을 중지합니다.
        if (carController == null)
        {
            Debug.LogError("CarUIController에 CarController가 연결되지 않았습니다!");
            this.enabled = false; // 스크립트 비활성화
            return;
        }

        // 1. 속도계 바늘 업데이트
        UpdateNeedle(speedometerNeedle, carController.GetCurrentSpeed(), maxSpeed);

        // 2. RPM 바늘 업데이트
        UpdateNeedle(rpmNeedle, carController.GetCurrentRPM(), maxRpm);

        // 3. 연료 바늘 업데이트 (최대값으로 CarController의 최대 연료량을 사용)
        UpdateNeedle(fuelNeedle, carController.GetCurrentFuel(), carController.GetMaxFuel());
    }

    // --- Private Methods ---

    /// <summary>
    /// 지정된 값에 따라 계기판 바늘을 회전시키는 공용 함수입니다.
    /// </summary>
    /// <param name="needle">회전시킬 바늘의 Transform</param>
    /// <param name="currentValue">현재 값 (예: 현재 속도)</param>
    /// <param name="maxValue">최대 값 (예: 최대 속도)</param>
    private void UpdateNeedle(Transform needle, float currentValue, float maxValue)
    {
        // 바늘 Transform이 할당되지 않았다면 실행하지 않습니다.
        if (needle == null) return;

        // 현재 값이 최대값에서 차지하는 비율을 계산합니다 (0.0 ~ 1.0 사이로 제한)
        // maxValue가 0이 되는 경우(예: 연료통이 없는 경우)를 대비해 0으로 나뉘는 것을 방지합니다.
        float percentage = (maxValue > 0) ? Mathf.Clamp01(currentValue / maxValue) : 0f;

        // 비율(percentage)에 따라 0일 때의 각도(zeroAngle)와 최대일 때의 각도(maxAngle) 사이의 값을 보간(Lerp)합니다.
        float targetAngle = Mathf.Lerp(zeroAngle, maxAngle, percentage);

        // 계산된 각도를 바늘의 Z축 로컬 회전값(localRotation)으로 적용합니다.
        // Quaternion.Euler를 사용하여 (0, 0, Z각도)의 회전값을 만듭니다.
        needle.localRotation = Quaternion.Euler(0, 0, targetAngle);
    }
}
