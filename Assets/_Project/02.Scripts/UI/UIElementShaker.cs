using UnityEngine;
using CarDrive.Common;
using CarDrive.Gameplay;

namespace CarDrive.UI
{
    /// <summary>
    /// 계기판 UI(RectTransform)에 주행 진동과 충격 흔들림을 줍니다.
    ///
    /// 어느 차량의 계기판인지는 부모의 <see cref="Vehicle"/>에서 찾습니다.
    /// 화면 고정 HUD처럼 차량 계층 밖에 있다면 지금 타고 있는 차량을 따릅니다.
    /// 예전에는 <c>FindObjectOfType&lt;CarController&gt;()</c>로 씬의 아무 차나 잡고
    /// <c>Input.GetAxis</c>를 직접 읽었습니다.
    ///
    /// 충격 효과(TriggerImpactShake)는 그 차량의 CarCollisionHandler가 호출합니다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    /// <summary>
    /// <b>이 컴포넌트는 Feel 로 넘기지 않았습니다.</b>
    ///
    /// 겉보기에는 순수한 연출이라 MMF_Player 로 대체할 수 있어 보이지만,
    /// LateUpdate 에서 <b>매 프레임</b> anchoredPosition 을 덮어씁니다.
    /// 엔진 상태와 조향 입력에 따라 계속 달라지는 <b>지속 진동</b>이기 때문입니다.
    /// 피드백은 "재생하고 끝나는" 물건이라 이런 지속 추종을 대신할 수 없고,
    /// 같은 값을 두고 다투면 늦게 실행된 쪽이 이겨 화면이 떨립니다.
    ///
    /// 그래서 충격 연출만 <see cref="onImpact"/> 로 열어 두었습니다.
    /// <b>위치를 건드리지 않는 피드백</b>(소리·색·플래시)은 여기에 붙여도 안전합니다.
    /// </summary>
    public class UIElementShaker : MonoBehaviour, IImpactShakable
    {
        // --- Public Member Variables ---

        /// <summary>
        /// 이 계기판이 속한 차량입니다.
        /// 비워두면 Awake에서 부모를 거슬러 찾고, 그래도 없으면 매 프레임 <see cref="Vehicle.Current"/>를 따릅니다.
        /// </summary>
        [Header("연동")]
        [Tooltip("이 계기판이 속한 차량. 비워두면 부모에서 찾고, 그래도 없으면 " +
                 "지금 타고 있는 차량을 따릅니다.")]
        public Vehicle vehicle;

        /// <summary>주행 진동으로 흔들리는 폭입니다. (앵커 좌표 단위)</summary>
        [Header("엔진/주행 진동 효과")]
        [Tooltip("주행 진동으로 흔들리는 폭")]
        public float vibrationAmount = 2.0f;

        /// <summary>주행 진동의 기본 속도입니다. 노이즈 샘플링 주기로 쓰입니다.</summary>
        [Tooltip("주행 진동의 기본 속도")]
        public float vibrationSpeed = 20f;

        /// <summary>조향 입력이 진동 세기에 더해지는 비율입니다.</summary>
        [Tooltip("조향 입력이 진동 세기에 더해지는 비율")]
        public float steeringVibrationFactor = 0.5f;

        /// <summary>시동을 거는 순간 진동 속도에 곱해지는 배율입니다.</summary>
        [Header("시동 진동 효과")]
        [Tooltip("시동을 거는 순간 진동 속도에 곱해지는 배율")]
        public float startupVibrationBoost = 2.0f;

        /// <summary>시동 진동이 평상시 속도로 잦아드는 속도입니다.</summary>
        [Tooltip("시동 진동이 평상시 속도로 잦아드는 속도")]
        public float startupVibrationDecayRate = 3.0f;

        /// <summary>충격 한 번이 더하는 흔들림의 세기입니다.</summary>
        [Header("외부 충격 효과")]
        [Tooltip("충격 한 번이 더하는 흔들림의 세기")]
        public float impactIntensity = 15.0f;

        /// <summary>충격 흔들림이 사그라드는 속도입니다.</summary>
        [Tooltip("충격 흔들림이 사그라드는 속도")]
        public float impactDecayRate = 5.0f;

        /// <summary>충격을 받았을 때 호출됩니다.</summary>
        [Header("이벤트")]
        [Tooltip("충격을 받았을 때. Feel 의 MMF_Player 를 여기에 연결하세요. " +
                 "단 위치(anchoredPosition)는 이 스크립트가 매 프레임 덮어쓰므로, " +
                 "위치를 건드리지 않는 피드백(소리·색·플래시)만 붙이세요.")]
        public UnityEngine.Events.UnityEvent onImpact;

        // --- Private Member Variables ---

        /// <summary>흔들 대상이 되는 이 UI의 RectTransform입니다.</summary>
        private RectTransform rectTransform;

        /// <summary>흔들림이 없을 때 돌아갈 원래 앵커 위치입니다.</summary>
        private Vector2 originalAnchoredPos;

        /// <summary>직전 프레임의 시동 상태입니다. 시동이 걸리는 순간을 잡아내는 데 씁니다.</summary>
        private bool wasEngineOnLastFrame = false;

        /// <summary>지금 적용 중인 진동 속도입니다. 시동 직후 잠시 빨라졌다가 평상시 값으로 돌아옵니다.</summary>
        private float currentVibrationSpeed = 0f;

        /// <summary>남아 있는 충격 흔들림의 양입니다. 매 프레임 impactDecayRate만큼 줄어듭니다.</summary>
        private float currentImpactBoost = 0f;

        // --- Unity Event Functions ---

        /// <summary>
        /// RectTransform과 원래 위치를 저장하고, 비어 있는 차량 참조를 부모에서 채웁니다.
        /// </summary>
        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            originalAnchoredPos = rectTransform.anchoredPosition;
            currentVibrationSpeed = vibrationSpeed;

            if (vehicle == null) vehicle = GetComponentInParent<Vehicle>(true);
        }

        /// <summary>
        /// 충격 흔들림과 주행 진동을 합쳐 계기판의 앵커 위치에 적용합니다.
        /// 다른 로직이 위치를 옮긴 뒤에 덮어써야 하므로 LateUpdate에서 처리합니다.
        /// </summary>
        void LateUpdate()
        {
            // 외부 충격 효과
            Vector2 impactOffset = Vector2.zero;
            if (currentImpactBoost > 0.01f)
            {
                float impactX = (Mathf.PerlinNoise(Time.time * vibrationSpeed * 5f, 100f) - 0.5f) * currentImpactBoost;
                float impactY = (Mathf.PerlinNoise(100f, Time.time * vibrationSpeed * 5f) - 0.5f) * currentImpactBoost;
                impactOffset = new Vector2(impactX, impactY);
                currentImpactBoost = Mathf.Lerp(currentImpactBoost, 0f, Time.deltaTime * impactDecayRate);
            }

            // 차량 계층 밖에 있는 HUD라면 지금 타고 있는 차를 따릅니다.
            Vehicle source = vehicle != null ? vehicle : Vehicle.Current;

            if (source == null || source.controller == null)
            {
                rectTransform.anchoredPosition = originalAnchoredPos + impactOffset;
                return;
            }

            // --- 주행 진동 효과 ---
            bool isEngineOn = source.controller.IsEngineOn;

            if (isEngineOn && !wasEngineOnLastFrame)
            {
                currentVibrationSpeed = vibrationSpeed * startupVibrationBoost;
            }
            else
            {
                currentVibrationSpeed = Mathf.Lerp(currentVibrationSpeed, vibrationSpeed, Time.deltaTime * startupVibrationDecayRate);
            }
            wasEngineOnLastFrame = isEngineOn;

            float intensityMultiplier = 0f;
            if (isEngineOn)
            {
                intensityMultiplier = 1.0f;

                // 조향 세기는 그 차량의 입력에서 읽습니다. (입력 소스를 하나로 유지)
                float steering = source.input != null ? Mathf.Abs(source.input.SteerInput) : 0f;
                intensityMultiplier += steering * steeringVibrationFactor;
            }

            float vibX = (Mathf.PerlinNoise(Time.time * currentVibrationSpeed, 0f) - 0.5f) * vibrationAmount * intensityMultiplier;
            float vibY = (Mathf.PerlinNoise(0f, Time.time * currentVibrationSpeed) - 0.5f) * vibrationAmount * intensityMultiplier;
            Vector2 vibrationOffset = new Vector2(vibX, vibY);

            // --- 최종 효과 적용 ---
            rectTransform.anchoredPosition = originalAnchoredPos + vibrationOffset + impactOffset;
        }

        // --- Public Methods ---

        /// <summary>
        /// 외부(CarCollisionHandler)에서 호출하여 강한 충격 효과를 발동시킵니다.
        /// </summary>
        public void TriggerImpactShake()
        {
            if (onImpact != null) onImpact.Invoke();

            currentImpactBoost += impactIntensity;
        }

        /// <summary>
        /// 충격을 받아 흔들립니다. <see cref="IImpactShakable"/> 구현입니다.
        /// 방향은 쓰지 않습니다. 계기판은 화면에 붙어 있어 충격 방향을 나타낼 축이 없습니다.
        /// </summary>
        /// <param name="worldDirection">충격 방향(월드). 여기서는 무시합니다.</param>
        /// <param name="scale">세기 배율</param>
        public void TriggerImpactShake(Vector3 worldDirection, float scale)
        {
            if (onImpact != null) onImpact.Invoke();

            currentImpactBoost += impactIntensity * scale;
        }
    }
}
