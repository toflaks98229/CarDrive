using UnityEngine;

/// <summary>
/// '카메라 리그'에 부착되어 타겟(운전석)을 부드럽게 따라다니는 스크립트입니다.
/// MainCamera는 이 오브젝트의 자식으로 두어야 합니다.
/// </summary>
public class CarCameraFollow : MonoBehaviour
{
    // --- Public Member Variables ---

    [Tooltip("카메라가 따라다닐 타겟 (예: 운전석 위치의 빈 오브젝트)")]
    public Transform target;

    [Header("추적 속도 설정")]
    [Tooltip("위치 추적의 부드러움 정도 (높을수록 빠르게 반응)")]
    public float positionSmoothSpeed = 10f;

    [Tooltip("회전 추적의 부드러움 정도 (높을수록 빠르게 반응)")]
    public float rotationSmoothSpeed = 8f;

    // --- Unity Event Functions ---

    /// <summary>
    /// 스크립트가 처음 활성화될 때 호출됩니다.
    /// </summary>
    void Start()
    {
        // 타겟이 할당되지 않았으면 경고를 출력하고 스크립트를 비활성화합니다.
        if (target == null)
        {
            Debug.LogError("CarCameraFollow: Target이 할당되지 않았습니다!");
            this.enabled = false;
        }
    }

    /// <summary>
    /// 매 프레임마다 호출됩니다. (LateUpdate 대신 Update를 사용해도 큰 문제는 없으나,
    /// 타겟의 움직임이 Update에서 완료된다면 LateUpdate가 더 부드러울 수 있습니다.)
    /// </summary>
    void Update()
    {
        // 타겟이 유효한지 확인합니다 (예: 타겟이 파괴된 경우 등)
        if (target == null) return;

        // 1. 위치를 부드럽게 추적 (Lerp: 선형 보간)
        // 현재 위치에서 타겟 위치로 (Time.deltaTime * speed) 비율만큼 부드럽게 이동합니다.
        transform.position = Vector3.Lerp(
            transform.position,
            target.position,
            Time.deltaTime * positionSmoothSpeed
        );

        // 2. 회전을 부드럽게 추적 (Slerp: 구면 선형 보간 - 회전에 더 적합)
        // 현재 회전에서 타겟 회전으로 (Time.deltaTime * speed) 비율만큼 부드럽게 회전합니다.
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            target.rotation,
            Time.deltaTime * rotationSmoothSpeed
        );
    }
}
