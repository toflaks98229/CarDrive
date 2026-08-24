using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 카메라 리그를 타겟(운전석)에 부드럽게 붙여 따라다닙니다.
    ///
    /// <b>카메라를 직접 움직이지 않습니다.</b> 이 컴포넌트는 리그에 붙고, MainCamera는
    /// 그 리그의 자식으로 둡니다. 그래야 흔들림·시야각 같은 다른 효과가 카메라를
    /// 건드려도 추적과 서로 부딪히지 않습니다.
    ///
    /// <b>위치는 Lerp, 회전은 Slerp로 따라갑니다.</b> 회전에 선형 보간을 쓰면 큰 각도를
    /// 돌 때 지름길로 가로질러 기울어집니다. 구면 보간이라야 각속도가 고릅니다.
    /// </summary>
    public class CarCameraFollow : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>
        /// 따라다닐 대상입니다. 보통 운전석 위치에 둔 빈 오브젝트입니다.
        ///
        /// 비어 있으면 <c>Start</c>에서 오류를 남기고 스스로를 끕니다.
        /// 매 프레임 null을 검사하며 도는 것보다 한 번에 멈추는 편이 낫기 때문입니다.
        /// </summary>
        [Tooltip("카메라가 따라다닐 타겟 (예: 운전석 위치의 빈 오브젝트)")]
        public Transform target;

        /// <summary>
        /// 위치가 타겟을 따라붙는 속도입니다. 클수록 빨리 따라붙습니다.
        ///
        /// 너무 작으면 가속할 때 카메라가 뒤로 처지고, 너무 크면 차의 잔진동이
        /// 그대로 화면에 옮겨집니다.
        /// </summary>
        [Header("추적 속도 설정")]
        [Tooltip("위치 추적의 부드러움 정도 (높을수록 빠르게 반응)")]
        public float positionSmoothSpeed = 10f;

        /// <summary>회전이 타겟을 따라붙는 속도입니다. 클수록 빨리 돌아갑니다.</summary>
        [Tooltip("회전 추적의 부드러움 정도 (높을수록 빠르게 반응)")]
        public float rotationSmoothSpeed = 8f;

        // --- Unity Event Functions ---

        /// <summary>
        /// 타겟이 배선되어 있는지 확인하고, 없으면 스스로를 끕니다.
        ///
        /// 타겟 없이 켜져 있으면 카메라가 원점에 붙박여 아무것도 안 보입니다.
        /// 조용히 그러는 것보다 오류를 남기고 멈추는 편이 원인을 빨리 찾습니다.
        /// </summary>
        void Start()
        {
            // 타겟이 할당되지 않았으면 경고를 출력하고 스크립트를 비활성화합니다.
            if (target == null)
            {
                GameLog.Error(GameLog.Channel.Player, "CarCameraFollow: Target이 할당되지 않았습니다!");
                this.enabled = false;
            }
        }

        /// <summary>
        /// 매 프레임 타겟의 위치와 회전을 향해 조금씩 다가갑니다.
        ///
        /// 타겟이 도중에 파괴되었을 수 있어 매번 다시 확인합니다.
        /// <c>Start</c>에서 껐어도 타겟이 나중에 사라지는 경우는 따로이기 때문입니다.
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
}
