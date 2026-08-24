using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 운전 입력을 읽어 값으로만 들고 있습니다.
    ///
    /// <b>여기서는 판단하지 않습니다.</b> 시동이 걸렸는지, 지금 입력을 써도 되는지는
    /// <see cref="CarController"/>가 정합니다. 이 컴포넌트는 "지금 무엇이 눌려 있는가"만
    /// 답합니다. 그래서 조작 방식을 바꿔도 차의 물리 코드는 그대로입니다.
    ///
    /// <b>오버레이 차단도 여기서 하지 않습니다.</b> 메뉴가 떠 있으면 <see cref="GameInput"/>이
    /// 0과 false를 내주므로, 조작을 놓은 것과 저절로 같아집니다.
    /// (예전에는 이 검사를 손으로 한 줄 더 썼습니다)
    ///
    /// <see cref="CarController"/>와 같은 GameObject에 두어야 합니다.
    /// </summary>
    public class CarInput : MonoBehaviour
    {
        // --- Public Properties ---

        /// <summary>좌우 조향 입력입니다. -1이 왼쪽 끝, 1이 오른쪽 끝입니다.</summary>
        [Tooltip("좌우 조향 입력 값 (-1.0 ~ 1.0)")]
        public float SteerInput { get; private set; }

        /// <summary>
        /// 전후 입력입니다. 1이 전진, -1이 후진입니다.
        ///
        /// 이것이 곧 가속을 뜻하지는 않습니다. 후진 기어에서 어떻게 쓸지,
        /// 시동이 꺼졌을 때 무시할지는 <see cref="CarController"/>가 정합니다.
        /// </summary>
        [Tooltip("전후 가속/후진 입력 값 (-1.0 ~ 1.0)")]
        public float ThrottleInput { get; private set; } // 'Vertical' 대신 'Throttle'로 명칭 변경

        /// <summary>브레이크를 밟고 있는지 여부입니다.</summary>
        [Tooltip("브레이크 입력 여부")]
        public bool IsBraking { get; private set; }

        // --- Unity Event Functions ---

        /// <summary>
        /// 매 프레임 조향·스로틀·브레이크를 다시 읽어 둡니다.
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

        /// <summary>
        /// 컴포넌트가 꺼질 때도 입력을 정리해 둡니다.
        /// </summary>
        void OnDisable()
        {
            ResetInput();
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
    }
}
