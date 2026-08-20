using UnityEngine;
using UnityEngine.Events;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 비가 올 때 하늘을 보고 있으면 빗물을 받아 마십니다.
    /// 갈증이 줄고, 그만큼 배뇨가 차오릅니다.
    ///
    /// 성립 조건은 셋입니다.
    ///  1. 비가 내리고 있을 것 (WeatherSystem의 RainIntensity)
    ///  2. 시선이 충분히 위를 향할 것
    ///  3. 머리 위가 뚫려 있을 것 (지붕·나무 밑에서는 비를 못 받습니다)
    ///
    /// <b>반드시 도보 리그(Player_OnFoot)에 붙이세요.</b>
    /// 차에 타면 그 오브젝트가 통째로 꺼지므로, 차 안에서 하늘을 봐도 마셔지지 않습니다.
    /// </summary>
    public class RainDrinking : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>상하 각도를 읽어 올 대상(보통 메인 카메라)입니다. 비워두면 Camera.main을 씁니다.</summary>
        [Header("연동")]
        [Tooltip("상하 각도를 읽어 올 대상(보통 메인 카메라). 비워두면 Camera.main을 씁니다.")]
        public Transform aimSource;

        /// <summary>갈증·배뇨를 반영할 니즈 시스템입니다. 비워두면 Start에서 씬을 검색합니다.</summary>
        [Tooltip("니즈 시스템. 비워두면 씬에서 자동으로 찾습니다.")]
        public NeedsSystem needsSystem;

        /// <summary>시선이 이 각도(도) 위로 올라가면 빗물을 받기 시작합니다.</summary>
        [Header("하늘 보기 판정")]
        [Tooltip("시선이 이 각도(도) 위로 올라가면 받기 시작합니다.")]
        [Range(0f, 89f)]
        public float lookStartAngle = 30f;

        /// <summary>이 각도(도)부터는 빗물을 최대로 받습니다.</summary>
        [Tooltip("이 각도(도)부터는 최대로 받습니다.")]
        [Range(0f, 89f)]
        public float lookFullAngle = 65f;

        /// <summary>
        /// 이 빗줄기 양에서 효과가 최대가 됩니다.
        /// 폭우는 4까지 올라가므로 1이면 보통 비에서도 충분히 받습니다.
        /// </summary>
        [Header("비 판정")]
        [Tooltip("이 값에서 효과가 최대가 됩니다. 폭우는 4까지 올라가므로 1이면 보통 비에서도 충분히 받습니다.")]
        public float rainForFullEffect = 1f;

        /// <summary>이 아래로는 빗줄기가 너무 가늘어 못 받는 것으로 봅니다.</summary>
        [Tooltip("이 아래로는 빗줄기가 너무 가늘어 못 받는 것으로 봅니다.")]
        public float minRainIntensity = 0.05f;

        /// <summary>머리 위로 광선을 쏴서 지붕·처마 아래인지 확인할지 여부입니다.</summary>
        [Header("머리 위 가림 검사")]
        [Tooltip("체크하면 머리 위로 광선을 쏴서 지붕·처마 아래인지 확인합니다.")]
        public bool requireOpenSky = true;

        /// <summary>이 거리(m) 안에 무언가 있으면 비를 못 받습니다.</summary>
        [Tooltip("이 거리(m) 안에 무언가 있으면 비를 못 받습니다.")]
        public float coverCheckDistance = 6f;

        /// <summary>가림으로 칠 레이어입니다. 플레이어 자신은 빼 두세요.</summary>
        [Tooltip("가림으로 칠 레이어. 플레이어 자신은 빼 두세요.")]
        public LayerMask coverMask = ~0;

        /// <summary>가림 검사 주기(초)입니다. 매 프레임 광선을 쏠 필요가 없습니다.</summary>
        [Tooltip("가림 검사 주기(초). 매 프레임 할 필요가 없습니다.")]
        public float coverCheckInterval = 0.25f;

        /// <summary>최대로 받을 때 초당 줄어드는 갈증입니다.</summary>
        [Header("효과 (최대일 때 초당)")]
        [Tooltip("초당 줄어드는 갈증")]
        public float thirstReliefPerSecond = 0.02f;

        /// <summary>최대로 받을 때 초당 오르는 배뇨입니다. 마신 만큼 나중에 돌려줘야 합니다.</summary>
        [Tooltip("초당 오르는 배뇨. 마신 만큼 나중에 돌려줘야 합니다.")]
        public float urineGainPerSecond = 0.012f;

        /// <summary>빗물을 받기 시작할 때 한 번 호출됩니다. (마시는 소리 등)</summary>
        [Header("이벤트")]
        [Tooltip("빗물을 받기 시작할 때 (마시는 소리 등)")]
        public UnityEvent onDrinkStart;

        /// <summary>빗물 받기를 멈출 때 한 번 호출됩니다.</summary>
        [Tooltip("빗물 받기를 멈출 때")]
        public UnityEvent onDrinkStop;

        // --- Public Properties ---

        /// <summary>지금 받고 있는 정도(0~1)입니다. 비의 굵기와 시선 각도를 곱한 값입니다.</summary>
        public float Amount { get; private set; }

        /// <summary>지금 빗물을 받고 있는지 여부입니다.</summary>
        public bool IsDrinking { get { return Amount > 0.001f; } }

        /// <summary>머리 위가 뚫려 있는지 여부입니다. (마지막 검사 결과)</summary>
        public bool IsUnderOpenSky { get; private set; }

        // --- Private Member Variables ---

        /// <summary>다음 가림 검사까지 남은 시간(초)입니다. 0 이하가 되면 광선을 다시 쏩니다.</summary>
        private float coverTimer;

        /// <summary>직전 프레임에 빗물을 받고 있었는지 여부입니다. 시작·정지 순간을 잡아내는 데 씁니다.</summary>
        private bool wasDrinking;

        // --- Unity Event Functions ---

        /// <summary>
        /// 니즈 시스템 참조를 채우고, 첫 검사 전까지는 하늘이 뚫려 있는 것으로 둡니다.
        /// </summary>
        void Start()
        {
            if (needsSystem == null) needsSystem = GameContext.Resolve<NeedsSystem>(this);
            if (needsSystem == null) Debug.LogWarning("RainDrinking: NeedsSystem을 찾지 못했습니다.", this);

            IsUnderOpenSky = true;
        }

        /// <summary>
        /// 받는 양을 0으로 되돌려 정지 이벤트가 빠지지 않게 합니다.
        /// </summary>
        void OnDisable()
        {
            // 차에 타는 등으로 꺼질 때 "마시는 중" 상태가 남지 않게 정리합니다.
            SetDrinking(0f);
        }

        /// <summary>
        /// 빗물을 받는 정도를 계산하고, 그만큼 갈증을 덜고 배뇨를 채웁니다.
        /// 입력이 막혀 있으면(오버레이 등) 받기를 멈춥니다.
        /// </summary>
        void Update()
        {
            if (GameInput.Suspended)
            {
                SetDrinking(0f);
                return;
            }

            if (needsSystem == null) return;

            float amount = CalculateAmount();
            SetDrinking(amount);

            if (amount <= 0f) return;

            float dt = Time.deltaTime;
            needsSystem.Satisfy(NeedType.Thirst, thirstReliefPerSecond * amount * dt);
            needsSystem.Add(NeedType.Urine, urineGainPerSecond * amount * dt);
        }

        // --- Private Methods ---

        /// <summary>
        /// 비의 굵기와 시선 각도를 곱해 받는 정도를 구합니다.
        /// 하나라도 0이면 0입니다.
        /// </summary>
        /// <returns>빗물을 받는 정도(0~1)</returns>
        private float CalculateAmount()
        {
            float rain = WeatherSystem.GetRainIntensity();
            if (rain < minRainIntensity) return 0f;

            float rain01 = rainForFullEffect > 0.0001f ? Mathf.Clamp01(rain / rainForFullEffect) : 1f;

            float look01 = CalculateLookFactor();
            if (look01 <= 0f) return 0f;

            if (!CheckOpenSky()) return 0f;

            return rain01 * look01;
        }

        /// <summary>
        /// 시선이 얼마나 하늘을 향하는지(0~1)입니다.
        /// </summary>
        /// <returns>lookStartAngle에서 0, lookFullAngle에서 1이 되는 값</returns>
        private float CalculateLookFactor()
        {
            if (aimSource == null)
            {
                aimSource = GameContext.MainCameraTransform;
                if (aimSource == null) return 0f;
            }

            float elevation = Mathf.Asin(Mathf.Clamp(aimSource.forward.y, -1f, 1f)) * Mathf.Rad2Deg;

            if (lookFullAngle <= lookStartAngle)
            {
                return elevation >= lookStartAngle ? 1f : 0f;
            }

            return Mathf.Clamp01(Mathf.InverseLerp(lookStartAngle, lookFullAngle, elevation));
        }

        /// <summary>
        /// 머리 위가 뚫려 있는지 확인합니다. 매 프레임 쏘지 않고 주기적으로만 갱신합니다.
        /// </summary>
        /// <returns>머리 위가 뚫려 있으면 true. 검사 주기가 되지 않았으면 직전 결과를 그대로 반환합니다.</returns>
        private bool CheckOpenSky()
        {
            if (!requireOpenSky) return true;

            coverTimer -= Time.deltaTime;
            if (coverTimer > 0f) return IsUnderOpenSky;

            coverTimer = Mathf.Max(0.02f, coverCheckInterval);

            Vector3 origin = aimSource != null ? aimSource.position : transform.position;

            // 판정은 SkyCover에 모아 두었습니다. WeatherExposure가 같은 규칙을 씁니다.
            IsUnderOpenSky = SkyCover.IsOpen(origin, coverCheckDistance, coverMask);

            return IsUnderOpenSky;
        }

        /// <summary>
        /// 상태를 갱신하고, 시작·정지 순간에만 이벤트를 던집니다.
        /// </summary>
        /// <param name="amount">지금 빗물을 받는 정도(0~1). 0이면 받기를 멈춘 것으로 봅니다.</param>
        private void SetDrinking(float amount)
        {
            Amount = amount;

            bool nowDrinking = IsDrinking;
            if (nowDrinking == wasDrinking) return;

            wasDrinking = nowDrinking;

            if (nowDrinking)
            {
                if (onDrinkStart != null) onDrinkStart.Invoke();
            }
            else
            {
                if (onDrinkStop != null) onDrinkStop.Invoke();
            }
        }
    }
}
