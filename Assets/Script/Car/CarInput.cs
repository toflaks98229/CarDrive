using UnityEngine;

/// <summary>
/// 사용자의 입력을 감지하는 역할만 전담하는 클래스입니다.
/// CarController는 이 클래스의 프로퍼티를 참조하여 입력 값을 가져옵니다.
/// 이 컴포넌트는 CarController와 같은 GameObject에 추가해야 합니다.
/// </summary>
public class CarInput : MonoBehaviour
{
    // --- Public Properties ---

    [Tooltip("좌우 조향 입력 값 (-1.0 ~ 1.0)")]
    public float SteerInput { get; private set; }

    [Tooltip("전후 가속/후진 입력 값 (-1.0 ~ 1.0)")]
    public float ThrottleInput { get; private set; } // 'Vertical' 대신 'Throttle'로 명칭 변경

    [Tooltip("브레이크 입력 여부")]
    public bool IsBraking { get; private set; }

    // --- Unity Event Functions ---

    /// <summary>
    /// 매 프레임마다 호출되어 입력을 업데이트합니다.
    /// </summary>
    void Update()
    {
        // CarController가 시동 상태에 따라 이 값을 사용할지 결정합니다.
        SteerInput = Input.GetAxis("Horizontal");
        ThrottleInput = Input.GetAxis("Vertical");
        IsBraking = Input.GetKey(KeyCode.Space);
    }
}
