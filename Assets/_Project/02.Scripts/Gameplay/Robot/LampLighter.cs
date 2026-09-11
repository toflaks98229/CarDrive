using UnityEngine;
using UnityEngine.Events;
using VContainer;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 길의 <b>죽은 등을 다시 켜며 걷는</b> 기계입니다.
    ///
    /// <b>메우는 구멍.</b> 로봇과 귀신이 <b>서로 무관했습니다</b>(<c>로봇_기획.md</c> 의
    /// 6번). 두 시스템이 나란히 있을 뿐 곱해지지 않았습니다. 불이 켜진 구간에서
    /// 귀신이 뜸해지면 두 시스템이 처음으로 만납니다.
    ///
    /// <b>이것이 셋 중 유일하게 플레이어가 지키고 싶어지는 기계입니다.</b> 정비 팔과
    /// 견인기는 값을 받아 가는 기계이고, 순찰기와 지킴이는 비켜 주거나 부수는
    /// 대상입니다. 등을 켜며 걷는 것만이 <b>내 편</b>입니다.
    ///
    /// ⚠ <b>낮에 일합니다.</b> 등은 어두워져야 켜지지만 <b>죽은 등을 고치는 일</b>은
    /// 밝을 때 하는 편이 낫습니다. 밤에만 일하면 플레이어가 이 기계를 만나는 시간이
    /// 곧 귀신을 만나는 시간과 겹쳐, 지킬 여유가 없습니다.
    ///
    /// <b>퇴근하면 마을로 돌아갑니다.</b> 순찰기와 같은 규칙입니다. 낮에는 길에서
    /// 일하는 모습으로, 밤에는 마을에 선 모습으로 만나게 됩니다 — 한 기계를
    /// <b>두 가지 모습</b>으로 만나야 그것이 사는 물건으로 읽힙니다.
    /// </summary>
    [AddComponentMenu("CarDrive/점등기 (LampLighter)")]
    public class LampLighter : MonoBehaviour
    {
        // --- Public Types ---

        /// <summary>지금 무엇을 하는 중인가.</summary>
        public enum Phase
        {
            /// <summary>고칠 등을 찾는 중입니다.</summary>
            Looking,

            /// <summary>죽은 등을 향해 걸어갑니다.</summary>
            Walking,

            /// <summary>등을 손보는 중입니다.</summary>
            Working,

            /// <summary>퇴근해 마을로 돌아가는 중입니다.</summary>
            OffDuty,
        }

        // --- Public Member Variables ---

        /// <summary>걸어 주는 부품입니다. 비워두면 자기 계층에서 찾습니다.</summary>
        [Header("부품")]
        [Tooltip("걸어 주는 부품. 비워두면 자기 계층에서 찾습니다")]
        public RobotDriver driver;

        /// <summary>있으면 손보는 동안 등을 향해 굽힙니다.</summary>
        [Tooltip("있으면 손보는 동안 등을 향해 굽힙니다")]
        public RobotTurret arm;

        /// <summary>
        /// 일을 시작하는 시각입니다(0~24).
        ///
        /// <b>왜 낮인가.</b> 등을 고치는 일은 보이는 때에 합니다. 그리고 이 기계는
        /// 셋 중 유일하게 <b>지키고 싶어져야 하는</b> 기계라, 플레이어가 그것을
        /// 만나는 시간이 귀신을 만나는 시간과 겹치면 안 됩니다.
        /// </summary>
        [Header("시각표")]
        [Tooltip("일을 시작하는 시각(0~24)")]
        [Range(0f, 24f)]
        public float onDutyHour = 7f;

        /// <summary>일을 마치는 시각입니다(0~24). 시작과 같으면 쉬지 않습니다.</summary>
        [Tooltip("일을 마치는 시각(0~24). 시작과 같으면 쉬지 않습니다")]
        [Range(0f, 24f)]
        public float offDutyHour = 18f;

        /// <summary>
        /// 퇴근해서 설 자리입니다. 비워 두면 <b>선 자리에</b> 섭니다.
        ///
        /// 마을에 두면 밤에 플레이어가 이 기계를 마을에서 만납니다.
        /// </summary>
        [Tooltip("퇴근해서 설 자리. 비워 두면 선 자리에 섭니다")]
        public Transform berth;

        /// <summary>이 안에서 죽은 등을 찾습니다(m).</summary>
        [Header("찾는 범위")]
        [Tooltip("이 안에서 죽은 등을 찾습니다(m)")]
        [Range(20f, 600f)]
        public float search = 300f;

        /// <summary>이만큼 다가가야 손이 닿습니다(m).</summary>
        [Tooltip("이만큼 다가가야 손이 닿습니다(m)")]
        [Range(2f, 20f)]
        public float reach = 6f;

        /// <summary>등 하나를 손보는 데 걸리는 시간(초)입니다.</summary>
        [Header("일하는 모습")]
        [Tooltip("등 하나를 손보는 데 걸리는 시간(초)")]
        [Range(0f, 30f)]
        public float workSeconds = 6f;

        /// <summary>다 켠 뒤 다음 등을 찾기까지 쉬는 시간(초)입니다.</summary>
        [Tooltip("다 켠 뒤 다음 등을 찾기까지 쉬는 시간(초)")]
        [Range(0f, 60f)]
        public float restSeconds = 4f;

        /// <summary>등을 살렸을 때 낼 소리입니다.</summary>
        [Header("소리")]
        [Tooltip("등을 살렸을 때 낼 소리")]
        public AudioClip fixedSound;

        /// <summary>소리 크기입니다.</summary>
        [Tooltip("소리 크기")]
        [Range(0f, 1f)]
        public float volume = 0.5f;

        /// <summary>등 하나를 살릴 때마다 부릅니다.</summary>
        [Header("알림")]
        public UnityEvent onLampLit;

        // --- Public Properties ---

        /// <summary>지금 하는 일입니다.</summary>
        public Phase Doing { get; private set; }

        /// <summary>지금 손보는 등입니다. 없으면 null 입니다.</summary>
        public StreetLamp Target { get; private set; }

        /// <summary>여태 살린 등의 수입니다.</summary>
        public int Lit { get; private set; }

        /// <summary>지금 근무 시간인가.</summary>
        public bool OnDuty { get; private set; }

        // --- Private Member Variables ---

        private IGameClock clock = NullGameClock.Instance;
        private float working;
        private float resting;

        // --- Unity Event Functions ---

        void Awake()
        {
            if (driver == null) driver = GetComponentInChildren<RobotDriver>(true);
            if (arm == null) arm = GetComponentInChildren<RobotTurret>(true);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // ⚠ <b>시각표를 먼저 봅니다.</b> 손보던 중에 퇴근 시각이 되면 그 등은
            // 두고 갑니다. 하던 일을 마치고 가게 두면 <b>퇴근이 일에 밀려</b>
            // 시각표가 있으나 마나가 됩니다.
            OnDuty = DayHours.Within(Hour(), onDutyHour, offDutyHour);

            if (!OnDuty)
            {
                Rest();
                return;
            }

            if (Doing == Phase.OffDuty) Doing = Phase.Looking;

            switch (Doing)
            {
                case Phase.Looking: Seek(dt); break;
                case Phase.Walking: Approach(); break;
                case Phase.Working: Work(dt); break;
            }
        }

        // --- Injection ---

        /// <summary>게임 시계를 받습니다. 시각표가 그것을 봅니다.</summary>
        /// <param name="gameClock">게임 시계</param>
        [Inject]
        public void Construct(IGameClock gameClock)
        {
            if (gameClock != null) clock = gameClock;
        }

        // --- Public Methods ---

        /// <summary>
        /// 그 등을 고치러 갈 것인가.
        ///
        /// <b>왜 갈라 두는가.</b> "죽었고, 내 손이 닿을 만한 거리에 있다" — 이 둘이
        /// 이 기계의 판단 전부입니다. 등도 로봇도 없이 확인할 수 있어야 합니다.
        /// </summary>
        /// <param name="broken">그 등이 죽었는가</param>
        /// <param name="away">그 등까지의 거리(m)</param>
        /// <param name="search">찾는 범위(m)</param>
        /// <returns>고치러 갈 만하면 true</returns>
        public static bool Worth(bool broken, float away, float search)
        {
            return broken && away <= search;
        }

        // --- Private Methods ---

        /// <summary>가장 가까운 죽은 등을 찾습니다.</summary>
        /// <param name="dt">지난 시간(초)</param>
        private void Seek(float dt)
        {
            if (resting > 0f)
            {
                resting -= dt;
                return;
            }

            StreetLamp best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < StreetLamp.All.Count; i++)
            {
                StreetLamp lamp = StreetLamp.All[i];
                if (lamp == null) continue;

                float sqr = (lamp.transform.position - transform.position).sqrMagnitude;
                if (!Worth(lamp.broken, Mathf.Sqrt(sqr), search)) continue;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = lamp;
            }

            if (best == null) return;

            Target = best;
            Doing = Phase.Walking;
        }

        /// <summary>등을 향해 걸어갑니다.</summary>
        private void Approach()
        {
            // 가는 동안 누가 켰거나 등이 사라졌으면 다른 것을 찾습니다.
            if (Target == null || !Target.broken) { Give(); return; }

            Vector3 to = Target.transform.position;
            if (driver != null) driver.SetDestination(to);

            if (Flat(to, transform.position) > reach) return;

            if (driver != null) driver.ClearDestination();

            working = workSeconds;
            Doing = Phase.Working;
        }

        /// <summary>등을 손봅니다.</summary>
        /// <param name="dt">지난 시간(초)</param>
        private void Work(float dt)
        {
            if (Target == null) { Give(); return; }

            if (arm != null) arm.AimAt(Target.transform.position);

            working -= dt;
            if (working > 0f) return;

            Target.Relight();
            Lit++;

            if (fixedSound != null)
            {
                OneShotAudioPool.Play(fixedSound, Target.transform.position, volume);
            }

            if (onLampLit != null) onLampLit.Invoke();

            Give();
        }

        /// <summary>
        /// 퇴근합니다. 격납고가 있으면 그리로, 없으면 선 자리에 섭니다.
        ///
        /// ⚠ <b>쉬는 시간과 하던 일을 되돌립니다.</b> 안 그러면 다음 날 출근했을 때
        /// 어제 손보던 중간부터 이어져, 이미 켜진 등 앞에서 한참 서 있습니다.
        /// 순찰기가 같은 일을 합니다.
        /// </summary>
        private void Rest()
        {
            if (Doing != Phase.OffDuty)
            {
                Give();
                Doing = Phase.OffDuty;
            }

            if (driver == null) return;

            if (berth == null) { driver.ClearDestination(); return; }

            driver.SetDestination(berth.position);
        }

        /// <summary>지금 시각입니다(0~24).</summary>
        private float Hour()
        {
            return Mathf.Repeat(clock.TotalMinutes / 60f, 24f);
        }

        /// <summary>손을 떼고 다음을 기다립니다.</summary>
        private void Give()
        {
            if (arm != null) arm.StopAiming();

            Target = null;
            resting = restSeconds;
            Doing = Phase.Looking;
        }

        /// <summary>높이를 뺀 거리입니다. 등은 기둥 위에 있습니다.</summary>
        /// <param name="a">한 자리</param>
        /// <param name="b">다른 자리</param>
        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;

            return Vector3.Distance(a, b);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.95f, 0.5f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, search);

            if (Target == null) return;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, Target.transform.position);
        }
    }
}
