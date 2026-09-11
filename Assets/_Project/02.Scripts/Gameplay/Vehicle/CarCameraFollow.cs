using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 카메라 리그를 타겟(운전석)에 붙여 앉힙니다.
    ///
    /// <b>카메라를 직접 움직이지 않습니다.</b> 이 컴포넌트는 리그에 붙고, MainCamera는
    /// 그 리그의 자식으로 둡니다. 그래야 흔들림·시야각 같은 다른 효과가 카메라를
    /// 건드려도 추적과 서로 부딪히지 않습니다.
    ///
    /// ⚠ <b>보간하지 않습니다. 운전석에 붙박입니다.</b>
    ///
    /// 예전에는 위치를 Lerp, 회전을 Slerp 로 따라갔습니다. 그런데 <b>그 보간은 한 번도
    /// 화면에 나온 적이 없습니다</b> — 메인 카메라가 이 리그가 아니라 세단에 직접 매달려
    /// 있어서, 이 컴포넌트가 움직이는 리그에는 아무것도 달려 있지 않았습니다.
    ///
    /// 카메라를 리그 아래로 옮기고 나니 그 보간이 처음으로 보이는데, <b>1인칭에서는
    /// 못 씁니다.</b> 계기판은 차의 자식이고 카메라는 아니므로, 카메라가 0.1초라도
    /// 처지면 20 m/s 에서 <b>2 m</b> 뒤처져 운전석이 아니라 뒷좌석을 보게 됩니다.
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
        /// 매 프레임 운전석 자리에 올라앉습니다.
        ///
        /// ⚠ <b>LateUpdate 입니다.</b> 차는 물리로 움직이므로, 이 프레임의 <b>최종</b>
        /// 자리를 읽으려면 모든 Update 가 끝난 뒤여야 합니다. Update 에서 읽으면
        /// 한 프레임 묵은 자리에 앉아 화면이 떨립니다.
        ///
        /// 타겟이 도중에 파괴되었을 수 있어 매번 다시 확인합니다.
        /// </summary>
        void LateUpdate()
        {
            if (target == null) return;

            transform.SetPositionAndRotation(target.position, target.rotation);
        }
    }
}
