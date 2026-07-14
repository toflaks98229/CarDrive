using UnityEngine;

/// <summary>
/// 휠 콜라이더의 물리 상태를 시각적 휠 모델(Transform)과 운전대에
/// 동기화하는 역할만 전담하는 클래스입니다.
/// 이 컴포넌트는 CarController와 같은 GameObject에 추가해야 합니다.
/// </summary>
public class CarVisuals : MonoBehaviour
{
    [Header("휠 콜라이더 (물리)")]
    public WheelCollider frontLeftWheelCollider;
    public WheelCollider frontRightWheelCollider;
    public WheelCollider rearLeftWheelCollider;
    public WheelCollider rearRightWheelCollider;

    [Header("휠 모델 (시각적)")]
    public Transform frontLeftWheelTransform;
    public Transform frontRightWheelTransform;
    public Transform rearLeftWheelTransform;
    public Transform rearRightWheelTransform;

    [Header("시각적 요소 (운전대)")]
    public Transform steeringWheelTransform;
    public float maxSteeringWheelAngle = 90f;

    // --- Private Member Variables ---
    private float maxSteerAngleFromData; // CarController가 설정해줄 값

    /// <summary>
    /// CarController가 Start()에서 호출하여 최대 조향각을 설정합니다.
    /// </summary>
    public void Initialize(float maxSteerAngle)
    {
        maxSteerAngleFromData = maxSteerAngle;
    }

    /// <summary>
    /// CarController가 LateUpdate()에서 호출하여 모든 시각적 요소를 업데이트합니다.
    /// </summary>
    public void UpdateVisuals(float currentSteerAngle)
    {
        UpdateSingleWheel(frontLeftWheelCollider, frontLeftWheelTransform);
        UpdateSingleWheel(frontRightWheelCollider, frontRightWheelTransform);
        UpdateSingleWheel(rearLeftWheelCollider, rearLeftWheelTransform);
        UpdateSingleWheel(rearRightWheelCollider, rearRightWheelTransform);
        UpdateSteeringWheel(currentSteerAngle);
    }

    /// <summary>
    /// 개별 휠 콜라이더의 상태를 휠 Transform에 적용합니다.
    /// (원본 CarController의 private 메서드)
    /// </summary>
    private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelTransform)
    {
        if (wheelTransform == null) return;

        Vector3 pos;
        Quaternion rot;
        wheelCollider.GetWorldPose(out pos, out rot);
        wheelTransform.position = pos;
        wheelTransform.rotation = rot;
    }

    /// <summary>
    /// 현재 조향 각도에 맞춰 운전대의 시각적 회전을 업데이트합니다.
    /// (원본 CarController의 private 메서드)
    /// </summary>
    private void UpdateSteeringWheel(float currentSteerAngle)
    {
        if (steeringWheelTransform != null && maxSteerAngleFromData != 0)
        {
            float steerRatio = currentSteerAngle / maxSteerAngleFromData;
            float steeringWheelAngle = steerRatio * maxSteeringWheelAngle;
            steeringWheelTransform.localRotation = Quaternion.Euler(0, steeringWheelAngle, 0);
        }
    }
}
