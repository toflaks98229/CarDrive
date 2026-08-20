using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
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
            //
            // 오버레이가 떠 있으면 GameInput이 0과 false를 내주므로, 여기서 따로 막지 않아도
            // 조작을 놓은 것과 같아집니다. (예전에는 이 검사를 손으로 한 줄 더 썼습니다)
            SteerInput = GameInput.Steer;
            ThrottleInput = GameInput.Throttle;
            IsBraking = GameInput.Brake;
        }

        // --- Public Methods ---

        /// <summary>
        /// 입력 값을 모두 0으로 되돌립니다.
        /// 하차할 때 이 컴포넌트를 꺼도 마지막 입력 값이 남아 차가 계속 달리는 것을 막습니다.
        /// </summary>
        public void ResetInput()
        {
            SteerInput = 0f;
            ThrottleInput = 0f;
            IsBraking = false;
        }

        /// <summary>
        /// 컴포넌트가 꺼질 때도 입력을 정리해 둡니다.
        /// </summary>
        void OnDisable()
        {
            ResetInput();
        }
    }
}
