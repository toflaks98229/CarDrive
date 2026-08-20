using UnityEngine;
using UnityEngine.UI; // UI 요소를 사용할 경우를 대비해 추가 (현재는 Transform만 사용)

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 속도, RPM, 연료 계기판의 바늘(Transform)을 회전시키는 클래스입니다.
    ///
    /// <b>어느 차의 계기판인지는 <see cref="Vehicle"/>이 정합니다.</b>
    /// 예전에는 <see cref="CarController"/>를 직접 들고 있었습니다. 차가 한 대일 때는 성립하지만,
    /// 두 대가 되는 순간 이 계기판이 어느 차를 가리키는지가 <b>씬 배선에만</b> 달려 있게 됩니다.
    /// 옮겨 타도 바늘은 원래 차를 계속 가리킵니다.
    ///
    /// 이제 <see cref="UIElementShaker"/>와 같은 방식으로 찾습니다.
    /// 계기판이 차량 계층 안에 있으면 그 차를, 차량 밖 HUD라면 지금 타고 있는 차를 따릅니다.
    /// </summary>
    public class CarUIController : MonoBehaviour
    {
        // --- Public Member Variables ---

        [Header("핵심 컴포넌트 연결")]
        [Tooltip("이 계기판이 속한 차량. 비워두면 부모에서 찾고, 그래도 없으면 지금 타고 있는 차를 따릅니다.")]
        public Vehicle vehicle;

        [Tooltip("예전 배선입니다. vehicle이 비어 있을 때 여기서 차량을 거슬러 찾습니다. " +
                 "새로 연결할 때는 위의 vehicle을 쓰세요.")]
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
        /// 이 계기판이 속한 차량을 찾아 둡니다.
        ///
        /// 배선 검사는 <b>여기서</b> 합니다. 예전에는 Update 안에서 검사하고 Debug.LogError를
        /// 찍은 뒤 스스로를 껐습니다. 한 프레임뿐이지만, 확인할 수 있는 것을 매 프레임 확인하는 자리에
        /// 두는 것은 맞지 않습니다.
        /// </summary>
        void Awake()
        {
            if (vehicle == null) vehicle = GetComponentInParent<Vehicle>(true);

            // 예전 배선을 승계합니다. 씬에 carController만 연결되어 있어도 그대로 동작합니다.
            if (vehicle == null && carController != null)
            {
                vehicle = carController.GetComponentInParent<Vehicle>();
            }

            // 여기서 못 찾아도 괜찮습니다. 차량 밖 HUD라면 Vehicle.Current를 따릅니다.
            // 다만 그 경우 탑승 전에는 가리킬 차가 없으므로, 의도한 것인지 알려는 둡니다.
            if (vehicle == null)
            {
                Debug.LogWarning("CarUIController: 이 계기판이 속한 Vehicle을 찾지 못했습니다. " +
                                 "탑승 중인 차량을 따라갑니다.", this);
            }
        }

        /// <summary>
        /// 매 프레임 바늘 세 개를 갱신합니다.
        /// </summary>
        void Update()
        {
            CarController source = ResolveController();

            // 걸어 다니는 중이라면 가리킬 차가 없습니다. 바늘을 0으로 내려 둡니다.
            // (예전 값을 그대로 두면 차에서 내려도 속도계가 80을 가리킨 채 멈춰 있습니다)
            if (source == null)
            {
                UpdateNeedle(speedometerNeedle, 0f, maxSpeed);
                UpdateNeedle(rpmNeedle, 0f, maxRpm);
                return;
            }

            // 1. 속도계 바늘 업데이트
            UpdateNeedle(speedometerNeedle, source.CurrentSpeed, maxSpeed);

            // 2. RPM 바늘 업데이트
            UpdateNeedle(rpmNeedle, source.CurrentRpm, maxRpm);

            // 3. 연료 바늘 업데이트 (최대값으로 그 차량의 최대 연료량을 사용)
            UpdateNeedle(fuelNeedle, source.CurrentFuel, source.MaxFuel);
        }

        // --- Private Methods ---

        /// <summary>
        /// 지금 값을 읽어 올 차량의 컨트롤러를 돌려줍니다.
        /// </summary>
        /// <returns>이 계기판의 차량, 없으면 지금 타고 있는 차량의 컨트롤러. 둘 다 없으면 null입니다.</returns>
        private CarController ResolveController()
        {
            // 차량 계층 밖에 있는 HUD라면 지금 타고 있는 차를 따릅니다.
            Vehicle source = vehicle != null ? vehicle : Vehicle.Current;
            return source != null ? source.controller : null;
        }

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
}
