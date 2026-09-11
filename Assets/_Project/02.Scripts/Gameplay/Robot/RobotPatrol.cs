using UnityEngine;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 로봇에게 <b>갈 곳과 갈 시각</b>을 줍니다.
    ///
    /// <b>왜 필요한가.</b> <see cref="RobotDriver"/> 는 "트랜스폼 하나를 향해 걸어간다"
    /// 가 전부입니다. 그래서 씬에 놓인 로봇은 제자리에 서 있거나 한 점으로 걸어가서
    /// 멈춥니다. 그것은 기계가 아니라 <b>장식</b>입니다.
    ///
    /// 이 세계의 로봇은 사냥꾼이 아니라 <b>아직 자기 일을 하고 있는 기반 시설</b>입니다
    /// (<c>로봇_기획.md</c> 의 규칙 하나). 자기 일이 있으려면 <b>돌아야 할 길</b>과
    /// <b>퇴근 시각</b>이 있어야 합니다. 플레이어는 그 길을 외워서 피해 다니게 됩니다 —
    /// 그것이 이 기계를 상대하는 방법입니다.
    ///
    /// <b>시각표는 <see cref="ShopSchedule"/> 과 같은 규칙입니다.</b> 여는 시각과 닫는
    /// 시각이 같으면 <b>24 시간</b>이고, 닫는 시각이 더 이르면 자정을 넘긴 것으로 봅니다.
    /// 마트가 이미 그렇게 돌고 있으므로 여기서 다른 규칙을 만들 이유가 없습니다.
    /// </summary>
    [DefaultExecutionOrder(-5)]
    [AddComponentMenu("CarDrive/로봇 순찰 (RobotPatrol)")]
    [RequireComponent(typeof(RobotDriver))]
    public class RobotPatrol : MonoBehaviour
    {
        // --- Constants ---

        private const float MinutesPerDay = 1440f;

        // --- Public Member Variables ---

        /// <summary>
        /// 돌아야 할 자리들입니다.
        ///
        /// <see cref="routeParent"/> 를 주면 그 자식들이 순서대로 길이 됩니다 —
        /// 씬에서 만들기에는 그쪽이 편합니다. 둘 다 주면 이쪽이 이깁니다.
        /// </summary>
        [Header("길")]
        [Tooltip("돌아야 할 자리들. 비우고 아래 부모를 주면 그 자식들이 길이 됩니다")]
        public Transform[] route;

        /// <summary>이 오브젝트의 자식들이 순서대로 길이 됩니다.</summary>
        [Tooltip("이 오브젝트의 자식들이 순서대로 길이 됩니다")]
        public Transform routeParent;

        /// <summary>
        /// 끝에 닿으면 처음으로 돌아갈 것인가.
        ///
        /// 끄면 <b>왔던 길을 되짚습니다.</b> 막다른 골목이나 다리 위처럼 한 줄로 난
        /// 길에서는 이쪽이 맞습니다 — 고리로 돌게 하면 길 밖으로 질러갑니다.
        /// </summary>
        [Tooltip("끄면 왔던 길을 되짚습니다. 한 줄로 난 길에서는 그쪽이 맞습니다")]
        public bool loop = true;

        /// <summary>한 자리에 닿으면 여기 적은 만큼 서 있습니다(초).</summary>
        [Tooltip("한 자리에 닿으면 서 있는 시간(초). 0 이면 안 쉬고 바로 다음으로")]
        [Range(0f, 120f)]
        public float dwellSeconds = 6f;

        /// <summary>
        /// 일을 시작하는 시각입니다.
        ///
        /// <b>여는 시각과 닫는 시각이 같으면 24 시간</b>입니다. 닫는 시각이 더 이르면
        /// 자정을 넘긴 것으로 봅니다(예: 20 시 출근, 6 시 퇴근).
        /// </summary>
        [Header("시각표")]
        [Tooltip("일을 시작하는 시각. 아래와 같으면 24시간 돕니다")]
        [Range(0f, 24f)]
        public float onDutyHour;

        /// <summary>일을 마치는 시각입니다.</summary>
        [Tooltip("일을 마치는 시각")]
        [Range(0f, 24f)]
        public float offDutyHour;

        /// <summary>
        /// 일이 끝나면 돌아가는 자리입니다.
        ///
        /// 비우면 <b>그 자리에 섭니다.</b> 격납고가 있는 로봇만 주면 됩니다.
        /// </summary>
        [Tooltip("일이 끝나면 돌아가는 자리. 비우면 그 자리에 섭니다")]
        public Transform berth;

        // --- Public Properties ---

        /// <summary>지금 일하는 시각인지 여부입니다.</summary>
        public bool OnDuty { get; private set; }

        /// <summary>지금 향하고 있는 자리의 번호입니다.</summary>
        public int StopIndex { get; private set; }

        // --- Private Member Variables ---

        private IGameClock clock = NullGameClock.Instance;
        private RobotDriver driver;
        private float resting;
        private int step = 1;

        // --- Unity Event Functions ---

        void Awake()
        {
            driver = GetComponent<RobotDriver>();
        }

        void Start()
        {
            if (clock == NullGameClock.Instance)
            {
                TimeSystem time = GameContext.Resolve<TimeSystem>(this);
                if (time != null) clock = time;
            }
        }

        void Update()
        {
            if (driver == null) return;

            float hour = Mathf.Repeat(clock.TotalMinutes, MinutesPerDay) / 60f;

            if (Tick(Time.deltaTime, hour, transform.position, out Vector3 go))
            {
                driver.SetDestination(go);
            }
        }

        // --- Public Methods ---

        /// <summary>의존성을 꽂습니다.</summary>
        /// <param name="gameClock">시각을 읽을 시계</param>
        public void Construct(IGameClock gameClock)
        {
            if (gameClock != null) clock = gameClock;
        }

        /// <summary>
        /// 한 걸음 진행하고 <b>지금 가야 할 자리</b>를 내줍니다.
        ///
        /// <b>왜 갈라 두는가.</b> 여기가 이 부품의 판단 전부입니다 — 시각표를 보고,
        /// 닿았는지 보고, 쉬는 시간을 세고, 다음 자리를 고릅니다. 그것을 확인하려고
        /// 로봇과 지형과 시계를 세울 이유가 없습니다.
        /// </summary>
        /// <param name="dt">지난 시간(초)</param>
        /// <param name="hour">지금 시각 (0~24)</param>
        /// <param name="here">로봇이 서 있는 자리</param>
        /// <param name="destination">가야 할 자리</param>
        /// <returns>갈 곳이 있으면 true. false 면 그 자리에 섭니다</returns>
        public bool Tick(float dt, float hour, Vector3 here, out Vector3 destination)
        {
            destination = here;
            OnDuty = IsOnDuty(hour);

            if (!OnDuty)
            {
                // 퇴근했습니다. 격납고가 있으면 그리로, 없으면 선 자리에 섭니다.
                //
                // ⚠ <b>쉬는 시간과 자리를 되돌립니다.</b> 안 그러면 다음 날 출근했을 때
                // 어제 쉬던 중간부터 이어져, 한 자리를 건너뛰거나 그 자리에서
                // 다시 한참 서 있습니다.
                resting = 0f;
                StopIndex = 0;
                step = 1;

                if (berth == null) return false;

                destination = berth.position;
                return true;
            }

            Transform[] stops = Stops();
            if (stops == null || stops.Length == 0) return false;

            StopIndex = Mathf.Clamp(StopIndex, 0, stops.Length - 1);
            Transform at = stops[StopIndex];
            if (at == null) return false;

            destination = at.position;

            // 아직 안 닿았으면 계속 갑니다.
            float reach = driver != null ? driver.arriveRadius : 1.2f;
            if ((here - destination).sqrMagnitude > reach * reach) return true;

            // 닿았습니다. 서 있을 시간을 셉니다.
            resting += dt;
            if (resting < dwellSeconds) return true;

            resting = 0f;
            StopIndex = NextStop(StopIndex, stops.Length, loop, ref step);
            destination = stops[StopIndex] != null ? stops[StopIndex].position : destination;

            return true;
        }

        /// <summary>
        /// 이 시각에 일하는가.
        ///
        /// <see cref="ShopSchedule.IsWithinHours"/> 와 <b>같은 규칙</b>입니다.
        /// </summary>
        /// <param name="hour">0~24</param>
        public bool IsOnDuty(float hour)
        {
            if (Mathf.Approximately(onDutyHour, offDutyHour)) return true;

            if (onDutyHour < offDutyHour) return hour >= onDutyHour && hour < offDutyHour;

            // 자정을 넘긴 근무입니다.
            return hour >= onDutyHour || hour < offDutyHour;
        }

        /// <summary>
        /// 다음 자리의 번호입니다.
        ///
        /// <paramref name="loopRoute"/> 가 거짓이면 <b>끝에서 되짚습니다.</b>
        /// 그때 <paramref name="direction"/> 의 부호가 뒤집힙니다.
        /// </summary>
        /// <param name="index">지금 번호</param>
        /// <param name="count">자리 수</param>
        /// <param name="loopRoute">고리로 돌 것인가</param>
        /// <param name="direction">+1 또는 -1. 되짚을 때 뒤집힙니다</param>
        public static int NextStop(int index, int count, bool loopRoute, ref int direction)
        {
            if (count <= 1) return 0;

            if (loopRoute)
            {
                direction = 1;
                return (index + 1) % count;
            }

            int want = index + direction;

            if (want >= count || want < 0)
            {
                direction = -direction;
                want = index + direction;
            }

            return Mathf.Clamp(want, 0, count - 1);
        }

        // --- Private Methods ---

        /// <summary>돌아야 할 자리들입니다.</summary>
        private Transform[] Stops()
        {
            if (route != null && route.Length > 0) return route;
            if (routeParent == null) return null;

            int count = routeParent.childCount;
            if (count == 0) return null;

            Transform[] made = new Transform[count];
            for (int i = 0; i < count; i++) made[i] = routeParent.GetChild(i);

            return made;
        }

        void OnDrawGizmosSelected()
        {
            Transform[] stops = Stops();
            if (stops == null || stops.Length == 0) return;

            Gizmos.color = new Color(0.4f, 0.85f, 1f, 0.9f);

            for (int i = 0; i < stops.Length; i++)
            {
                if (stops[i] == null) continue;

                Gizmos.DrawWireSphere(stops[i].position, 1.2f);

                Transform next = stops[(i + 1) % stops.Length];
                if (next == null) continue;
                if (!loop && i == stops.Length - 1) continue;

                Gizmos.DrawLine(stops[i].position, next.position);
            }

            if (berth == null) return;

            Gizmos.color = new Color(1f, 0.75f, 0.3f, 0.9f);
            Gizmos.DrawWireCube(berth.position, Vector3.one * 2f);
        }
    }
}
