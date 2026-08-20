using UnityEngine;

namespace CarDrive.Common
{
    /// <summary>
    /// 이 오브젝트가 항상 메인 카메라를 바라보도록 만듭니다.
    /// 주로 3D 공간의 2D 스프라이트(예: 이름표, 파티클)에 사용됩니다.
    /// [수정됨] 축 잠금 기능이 추가되었습니다.
    /// </summary>
    public class Billboard : MonoBehaviour
    {
        // --- Public Member Variables ---

        [Header("축 잠금 설정")]
        [Tooltip("X축 회전을 잠급니다 (상하 회전 고정)")]
        public bool lockXAxis = false;

        [Tooltip("Y축 회전을 잠급니다 (좌우 회전 고정)")]
        public bool lockYAxis = false;

        [Tooltip("Z축 회전을 잠급니다 (기울임 고정)")]
        public bool lockZAxis = false;

        // --- Private Member Variables ---

        /// <summary>
        /// 메인 카메라의 Transform 컴포넌트 (성능을 위해 캐시됨)
        /// </summary>
        private Transform mainCameraTransform;

        // --- Unity Event Functions ---

        /// <summary>
        /// 스크립트가 처음 활성화될 때 호출됩니다.
        /// </summary>
        void Start()
        {
            // 메인 카메라를 찾아서 Transform을 캐시(저장)합니다.
            // 이 스크립트가 작동하려면 씬에 'MainCamera' 태그가 붙은 카메라가 있어야 합니다.
            if (GameContext.MainCamera != null)
            {
                mainCameraTransform = GameContext.MainCameraTransform;
            }
            else
            {
                Debug.LogError("Billboard: 씬에서 'MainCamera' 태그를 가진 카메라를 찾을 수 없습니다!");
                this.enabled = false; // 카메라가 없으면 스크립트 비활성화
            }
        }

        /// <summary>
        /// LateUpdate는 모든 Update와 카메라 이동이 끝난 후에 호출됩니다.
        /// 카메라가 움직인 '후'에 오브젝트의 회전을 조절해야 올바르게 작동합니다.
        /// </summary>
        void LateUpdate()
        {
            // 카메라 Transform이 유효하지 않으면 실행하지 않습니다.
            if (mainCameraTransform == null) return;

            // 목표 회전값 (카메라의 현재 회전)
            Quaternion targetRotation = mainCameraTransform.rotation;

            // 현재 회전값 (오브젝트의 현재 회전)을 오일러 각도로 변환
            Vector3 currentEulerAngles = transform.rotation.eulerAngles;

            // 목표 회전값 (오일러 각도)
            Vector3 targetEulerAngles = targetRotation.eulerAngles;

            // bool 값에 따라 축을 선택적으로 적용
            // 잠금(true)이면 현재 각도를 유지하고, 아니면(false) 목표 각도를 따름
            float finalX = lockXAxis ? currentEulerAngles.x : targetEulerAngles.x;
            float finalY = lockYAxis ? currentEulerAngles.y : targetEulerAngles.y;
            float finalZ = lockZAxis ? currentEulerAngles.z : targetEulerAngles.z;

            // 최종 계산된 오일러 각도를 쿼터니언으로 변환하여 오브젝트의 회전값으로 적용
            transform.rotation = Quaternion.Euler(finalX, finalY, finalZ);
        }
    }
}
