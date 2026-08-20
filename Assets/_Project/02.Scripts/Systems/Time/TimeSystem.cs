using UnityEngine;
using UnityEngine.Events;
using CarDrive.Common;

namespace CarDrive.Systems
{
    /// <summary>하루의 시간대 구분입니다.</summary>
    public enum DayPhase
    {
        Dawn,       // 새벽
        Morning,    // 아침
        Afternoon,  // 낮
        Evening,    // 저녁
        Night       // 밤
    }

    /// <summary>DayPhase 하나를 넘기는 인스펙터 연결용 이벤트입니다.</summary>
    [System.Serializable]
    public class DayPhaseEvent : UnityEvent<DayPhase> { }

    /// <summary>int 하나를 넘기는 인스펙터 연결용 이벤트입니다.</summary>
    [System.Serializable]
    public class IntEvent : UnityEvent<int> { }

    /// <summary>
    /// 하루의 시간 흐름과 주야 구분을 관리합니다.
    ///
    /// 니즈·날씨처럼 시간에 따라 진행되는 시스템들이 각자 시간을 세지 않고
    /// 여기 한 곳에서 읽어 가도록 하는 것이 목적입니다.
    /// (그래야 수면으로 시간을 건너뛸 때 모든 시스템이 같이 움직입니다)
    /// </summary>
    public class TimeSystem : MonoBehaviour, ISaveable
    {
        public const float MinutesPerDay = 1440f;

        // --- Static Access ---

        /// <summary>씬의 시간 시스템입니다. 없으면 각 시스템이 자체 배율로 돌아갑니다.</summary>
        public static TimeSystem Instance { get { return GameContext.Get<TimeSystem>(); } }

        // --- Public Member Variables ---

        [Header("시간 흐름")]
        [Tooltip("실제 1초당 흐르는 게임 시간(분). 1이면 실제 24분이 게임 하루입니다.")]
        public float gameMinutesPerRealSecond = 1f;

        [Tooltip("체크하면 시간이 멈춥니다.")]
        public bool paused = false;

        [Header("시작 시각")]
        [Range(0f, 24f)]
        public float startHour = 8f;

        [Tooltip("시작 날짜 (1일차부터)")]
        public int startDay = 1;

        [Header("주야 구분 (시)")]
        [Tooltip("새벽 시작")]
        public float dawnStartHour = 5f;
        [Tooltip("아침 시작")]
        public float morningStartHour = 7f;
        [Tooltip("낮 시작")]
        public float afternoonStartHour = 12f;
        [Tooltip("저녁 시작")]
        public float eveningStartHour = 18f;
        [Tooltip("밤 시작")]
        public float nightStartHour = 21f;

        [Header("햇빛")]
        [Tooltip("해가 완전히 뜬 것으로 보는 시각")]
        public float fullDaylightHour = 9f;
        [Tooltip("해가 완전히 진 것으로 보는 시각")]
        public float fullDarkHour = 20f;

        [Tooltip("회전시킬 태양광. 비워두면 조명은 건드리지 않습니다.")]
        public Light sunLight;

        [Tooltip("태양광의 최대 밝기. DaylightFactor에 비례해 줄어듭니다.")]
        public float sunMaxIntensity = 1.2f;

        [Header("이벤트")]
        public IntEvent onDayChanged;
        public IntEvent onHourChanged;
        public DayPhaseEvent onPhaseChanged;

        // --- Public Properties ---

        /// <summary>며칠째인지 (1부터).</summary>
        public int Day { get; private set; }

        /// <summary>하루 중 몇 분이 지났는지 (0~1440).</summary>
        public float MinuteOfDay { get; private set; }

        /// <summary>하루 중 몇 시인지 (0~24, 소수 포함).</summary>
        public float Hour { get { return MinuteOfDay / 60f; } }

        /// <summary>하루를 0~1로 표현한 값입니다.</summary>
        public float NormalizedDay { get { return MinuteOfDay / MinutesPerDay; } }

        /// <summary>현재 시간대입니다.</summary>
        public DayPhase Phase { get; private set; }

        /// <summary>밤(또는 새벽)인지 여부입니다. 귀신 활동·시야 판정에 씁니다.</summary>
        public bool IsNight { get { return Phase == DayPhase.Night || Phase == DayPhase.Dawn; } }

        /// <summary>0이면 한밤, 1이면 한낮입니다. 조명·시야 계산에 씁니다.</summary>
        public float DaylightFactor { get; private set; }

        /// <summary>지금까지 흐른 총 게임 시간(분)입니다. 날씨 간격 계산 등에 씁니다.</summary>
        public float TotalMinutes { get { return (Day - 1) * MinutesPerDay + MinuteOfDay; } }

        // --- Private Member Variables ---

        /// <summary>직전에 알린 정각입니다. 시가 바뀌는 순간에만 이벤트를 던지는 데 씁니다.</summary>
        private int lastHour = -1;

        /// <summary>직전에 알린 시간대입니다. 시간대가 바뀌는 순간에만 이벤트를 던지는 데 씁니다.</summary>
        private DayPhase lastPhase;

        // --- Unity Event Functions ---

        /// <summary>
        /// 자신을 전역 인스턴스로 등록하고 시작 날짜·시각과 시간대·햇빛을 맞춥니다.
        /// 이미 다른 인스턴스가 있으면 경고를 남기고 자신을 끕니다.
        /// </summary>
        void Awake()
        {
            // 등록이 거부되면 이미 다른 것이 있다는 뜻입니다. (경고는 GameContext가 남깁니다)
            if (!GameContext.Register(this))
            {
                enabled = false;
                return;
            }

            // 세이브에도 참여합니다. SaveSystem 은 이 등록만 보고 훑으므로,
            // 이쪽에서 자신을 넣지 않으면 시각이 저장되지 않습니다.
            SaveRegistry.Register(this);

            Day = Mathf.Max(1, startDay);
            MinuteOfDay = Mathf.Repeat(startHour * 60f, MinutesPerDay);

            Phase = CalculatePhase(Hour);
            lastPhase = Phase;
            lastHour = Mathf.FloorToInt(Hour);
            UpdateDaylight();
        }

        /// <summary>
        /// 자신이 전역 인스턴스였다면 그 참조를 비웁니다.
        /// </summary>
        void OnDestroy()
        {
            GameContext.Unregister(this);
            SaveRegistry.Unregister(this);
        }

        // --- ISaveable ---

        /// <summary>시계가 가장 먼저입니다. 니즈와 날씨가 이 시각을 보고 움직입니다.</summary>
        public int SaveOrder { get { return SaveOrders.Time; } }

        /// <summary>날짜와 시각을 세이브에 적습니다.</summary>
        /// <param name="data">적어 넣을 세이브 자료</param>
        public void CaptureInto(SaveData data)
        {
            data.time.day = Day;
            data.time.minuteOfDay = MinuteOfDay;
        }

        /// <summary>세이브에서 날짜와 시각을 되돌립니다.</summary>
        /// <param name="data">읽어 올 세이브 자료</param>
        public void RestoreFrom(SaveData data)
        {
            RestoreClock(data.time.day, data.time.minuteOfDay);
        }

        /// <summary>
        /// 배율만큼 게임 시간을 흘려보냅니다. 일시정지 중이면 아무 일도 하지 않습니다.
        /// </summary>
        void Update()
        {
            if (paused) return;
            AdvanceMinutes(Time.deltaTime * gameMinutesPerRealSecond);
        }

        // --- Public Methods ---

        /// <summary>
        /// 시간을 흘려보냅니다. 수면처럼 크게 건너뛸 때도 이 메서드를 씁니다.
        /// </summary>
        public void AdvanceMinutes(float minutes)
        {
            if (minutes <= 0f) return;

            MinuteOfDay += minutes;

            // 하루를 넘겼으면 날짜를 올립니다. (한 번에 여러 날을 건너뛸 수도 있습니다)
            while (MinuteOfDay >= MinutesPerDay)
            {
                MinuteOfDay -= MinutesPerDay;
                Day++;
                if (onDayChanged != null) onDayChanged.Invoke(Day);
            }

            int hour = Mathf.FloorToInt(Hour);
            if (hour != lastHour)
            {
                lastHour = hour;
                if (onHourChanged != null) onHourChanged.Invoke(hour);
            }

            Phase = CalculatePhase(Hour);
            if (Phase != lastPhase)
            {
                lastPhase = Phase;
                if (onPhaseChanged != null) onPhaseChanged.Invoke(Phase);
            }

            UpdateDaylight();
        }

        /// <summary>
        /// 세이브에서 읽은 시각으로 시계를 되돌립니다.
        /// AdvanceMinutes와 달리 이벤트를 발생시키지 않고 상태만 맞춥니다.
        /// (불러오는 순간 하루가 지났다는 알림이 뜨면 곤란하기 때문입니다)
        /// </summary>
        public void RestoreClock(int day, float minuteOfDay)
        {
            Day = Mathf.Max(1, day);
            MinuteOfDay = Mathf.Repeat(minuteOfDay, MinutesPerDay);

            Phase = CalculatePhase(Hour);
            lastPhase = Phase;
            lastHour = Mathf.FloorToInt(Hour);

            UpdateDaylight();
        }

        /// <summary>지정한 시각으로 건너뜁니다. 이미 지난 시각이면 다음 날로 넘어갑니다.</summary>
        public void SkipToHour(float hour)
        {
            float targetMinute = Mathf.Repeat(hour * 60f, MinutesPerDay);
            float delta = targetMinute - MinuteOfDay;
            if (delta <= 0f) delta += MinutesPerDay;

            AdvanceMinutes(delta);
        }

        /// <summary>"07:35" 형태의 시계 문자열입니다.</summary>
        public string GetClockText()
        {
            int h = Mathf.FloorToInt(MinuteOfDay / 60f);
            int m = Mathf.FloorToInt(MinuteOfDay % 60f);
            return h.ToString("00") + ":" + m.ToString("00");
        }

        /// <summary>시간대의 한글 이름입니다.</summary>
        public string GetPhaseName()
        {
            switch (Phase)
            {
                case DayPhase.Dawn: return "새벽";
                case DayPhase.Morning: return "아침";
                case DayPhase.Afternoon: return "낮";
                case DayPhase.Evening: return "저녁";
                default: return "밤";
            }
        }

        /// <summary>
        /// 시간 시스템이 없어도 동작하도록, 참조 없이 배율을 읽는 편의 메서드입니다.
        /// </summary>
        public static float GetMinutesPerSecond(float fallback)
        {
            return Instance != null ? Instance.gameMinutesPerRealSecond : fallback;
        }

        /// <summary>참조 없이 밤인지 확인합니다. 시스템이 없으면 false입니다.</summary>
        public static bool IsNightNow()
        {
            return Instance != null && Instance.IsNight;
        }

        /// <summary>참조 없이 낮 밝기를 읽습니다. 시스템이 없으면 1(대낮)입니다.</summary>
        public static float GetDaylight()
        {
            return Instance != null ? Instance.DaylightFactor : 1f;
        }

        // --- Private Methods ---

        /// <summary>
        /// 시각을 인스펙터에 설정된 경계와 비교해 어느 시간대인지 판정합니다.
        /// </summary>
        /// <param name="hour">판정할 시각(0~24)</param>
        /// <returns>해당 시각이 속한 시간대</returns>
        private DayPhase CalculatePhase(float hour)
        {
            if (hour >= nightStartHour || hour < dawnStartHour) return DayPhase.Night;
            if (hour < morningStartHour) return DayPhase.Dawn;
            if (hour < afternoonStartHour) return DayPhase.Morning;
            if (hour < eveningStartHour) return DayPhase.Afternoon;
            return DayPhase.Evening;
        }

        /// <summary>
        /// 해가 뜨고 지는 구간을 부드럽게 이어 밝기를 구하고, 태양광이 있으면 함께 돌립니다.
        /// </summary>
        private void UpdateDaylight()
        {
            float hour = Hour;

            if (hour <= dawnStartHour || hour >= fullDarkHour + (nightStartHour - fullDarkHour))
            {
                DaylightFactor = 0f;
            }
            else if (hour < fullDaylightHour)
            {
                // 새벽~완전히 밝아질 때까지
                DaylightFactor = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(dawnStartHour, fullDaylightHour, hour));
            }
            else if (hour < eveningStartHour)
            {
                DaylightFactor = 1f;
            }
            else
            {
                // 저녁~완전히 어두워질 때까지
                DaylightFactor = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(eveningStartHour, fullDarkHour, hour));
            }

            if (sunLight != null)
            {
                // 06시에 동쪽 지평선, 12시에 머리 위, 18시에 서쪽 지평선이 되도록 돌립니다.
                float sunAngle = (MinuteOfDay / MinutesPerDay) * 360f - 90f;
                sunLight.transform.rotation = Quaternion.Euler(sunAngle, 170f, 0f);
                sunLight.intensity = sunMaxIntensity * DaylightFactor;
                sunLight.enabled = DaylightFactor > 0.01f;
            }
        }
    }
}
