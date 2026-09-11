using UnityEngine;
using UnityEngine.Events;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 로봇이 사람을 <b>알아채되 쫓지 않게</b> 합니다.
    ///
    /// <b>규칙 하나가 이 부품의 모양을 정합니다</b>(<c>로봇_기획.md</c>) —
    /// "귀신은 당신을 노립니다. <b>로봇은 당신을 모릅니다.</b>"
    /// 그래서 여기서 만드는 것은 추적이 아니라 <b>반응</b>입니다. 셋뿐이고
    /// <b>무시가 기본값</b>입니다.
    ///
    /// <list type="bullet">
    /// <item><b>무시</b> — 거의 언제나 이것입니다. 기계는 제 일을 계속합니다.</item>
    /// <item><b>알아챔</b> — 가까이 앞에 있으면 고개가 따라옵니다. 그뿐입니다.</item>
    /// <item><b>항의</b> — <b>일을 못 하게 됐을 때</b>입니다. 서서 돌아보고 항의합니다.</item>
    /// </list>
    ///
    /// <b>항의가 이 설계의 핵심입니다.</b> 로봇이 화내는 이유는 "적을 봤다" 가 아니라
    /// <b>"가야 하는데 못 간다"</b> 입니다. 차를 길 한복판에 세워 두면 항의를 듣고,
    /// 비켜 주면 조용해집니다. 쫓기는 것이 아니라 <b>길을 두고 다투는 것</b>이라,
    /// 앙크가 상대하는 귀신과 축이 겹치지 않습니다.
    ///
    /// ⚠ <b>싸움은 여기서 시작하지 않습니다.</b> 적대적인 로봇은 드레드노트 하나뿐이고,
    /// 그것은 <see cref="RobotThreat"/> 가 <c>Provoke</c> 로 따로 다룹니다.
    /// 여기서 하는 것은 그 앞 단계까지입니다.
    /// </summary>
    [DefaultExecutionOrder(-4)]
    [AddComponentMenu("CarDrive/로봇 지각 (RobotAwareness)")]
    public class RobotAwareness : MonoBehaviour
    {
        // --- Public Types ---

        /// <summary>기계가 지금 사람을 어떻게 여기는가.</summary>
        public enum Response
        {
            /// <summary>모릅니다. <b>기본값입니다.</b></summary>
            Ignore = 0,

            /// <summary>알아챘습니다. 고개만 따라갑니다.</summary>
            Notice,

            /// <summary>일을 못 하게 됐습니다. 서서 항의합니다.</summary>
            Protest,
        }

        /// <summary>한 번에 잰 것들입니다.</summary>
        public struct Sense
        {
            /// <summary>사람까지의 거리(m)입니다.</summary>
            public float Distance;

            /// <summary>기계가 보는 쪽에서 몇 도 벗어나 있는가.</summary>
            public float Angle;

            /// <summary>사이에 가리는 것이 없는가.</summary>
            public bool InSight;

            /// <summary>갈 곳이 있는가. 없으면 막힐 일도 없습니다.</summary>
            public bool Working;

            /// <summary>가려는데 못 간 시간(초)입니다.</summary>
            public float Stuck;

            /// <summary>얻어맞은 지 얼마나 되었는가(초). 맞은 적 없으면 큰 값.</summary>
            public float SinceHit;
        }

        // --- Public Member Variables ---

        /// <summary>이 안에 들어오면 알아챕니다(m).</summary>
        [Header("알아챔")]
        [Tooltip("이 안에 들어오면 알아챕니다(m)")]
        [Range(3f, 80f)]
        public float noticeRadius = 26f;

        /// <summary>보는 쪽에서 이 각도 안이어야 알아챕니다.</summary>
        [Tooltip("보는 쪽에서 이 각도 안이어야 알아챕니다")]
        [Range(10f, 180f)]
        public float noticeAngle = 75f;

        /// <summary>
        /// 가려는데 못 간 시간이 이만큼이면 항의합니다(초).
        ///
        /// ⚠ <b>짧게 주면 안 됩니다.</b> 보행기는 한 걸음 사이에 잠깐 멈춥니다.
        /// 그 순간을 막힌 것으로 세면 <b>아무도 앞에 없는데 항의합니다.</b>
        /// </summary>
        [Header("항의")]
        [Tooltip("가려는데 못 간 시간이 이만큼이면 항의합니다(초). 짧으면 헛항의합니다")]
        [Range(0.5f, 12f)]
        public float patience = 3.5f;

        /// <summary>이 속도 아래면 못 가고 있는 것으로 봅니다(m/s).</summary>
        [Tooltip("이 속도 아래면 못 가고 있는 것으로 봅니다(m/s)")]
        [Range(0.01f, 1f)]
        public float stuckSpeed = 0.25f;

        /// <summary>사람이 이 안에 있을 때만 사람 탓으로 봅니다(m).</summary>
        [Tooltip("사람이 이 안에 있을 때만 막은 것이 사람이라고 봅니다(m)")]
        [Range(3f, 40f)]
        public float blameRadius = 14f;

        /// <summary>얻어맞으면 이 시간 동안 항의합니다(초).</summary>
        [Tooltip("얻어맞으면 이 시간 동안 항의합니다(초)")]
        [Range(1f, 30f)]
        public float grudgeSeconds = 8f;

        /// <summary>무엇이 시야를 가리는가.</summary>
        [Header("보는 법")]
        [Tooltip("무엇이 시야를 가리는가")]
        public LayerMask sightMask = ~0;

        /// <summary>눈높이입니다(m). 발밑에서 재면 땅에 가립니다.</summary>
        [Tooltip("눈높이(m). 발밑에서 재면 제 땅에 가립니다")]
        public float eyeHeight = 6f;

        /// <summary>반응이 바뀔 때 부릅니다. 소리와 자세를 여기에 답니다.</summary>
        [Header("알림")]
        public UnityEvent onNoticed;

        /// <summary>항의를 시작할 때 부릅니다.</summary>
        public UnityEvent onProtested;

        /// <summary>다시 제 일로 돌아갈 때 부릅니다.</summary>
        public UnityEvent onCalmed;

        // --- Public Properties ---

        /// <summary>지금의 반응입니다.</summary>
        public Response State { get; private set; }

        // --- Private Member Variables ---

        private RobotDriver driver;
        private RobotPatrol patrol;
        private Transform player;

        private float stuck;
        private float sinceHit = 9999f;

        // --- Unity Event Functions ---

        void Awake()
        {
            driver = GetComponent<RobotDriver>();
            patrol = GetComponent<RobotPatrol>();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            sinceHit += dt;

            Transform who = Player();
            Sense now = Look(who, dt);

            Response want = Judge(now, noticeRadius, noticeAngle, patience, blameRadius);
            Apply(want);
        }

        // --- Public Methods ---

        /// <summary>
        /// 얻어맞았습니다. 잠시 항의합니다.
        ///
        /// <see cref="RobotThreat.Provoke"/> 와 나란히 두는 것입니다 — 저쪽은 싸움을
        /// 열고 이쪽은 <b>소리를 냅니다.</b> 안 싸우는 기계도 맞으면 반응해야 합니다.
        /// </summary>
        public void Bumped()
        {
            sinceHit = 0f;
        }

        /// <summary>
        /// 잰 것으로 반응을 고릅니다.
        ///
        /// <b>왜 갈라 두는가.</b> 여기가 판단 전부입니다. 로봇도 지형도 없이 확인할 수
        /// 있어야, "왜 항의했는가" 를 따질 때 씬을 뒤지지 않습니다.
        /// </summary>
        /// <param name="sense">잰 것</param>
        /// <param name="radius">알아채는 거리</param>
        /// <param name="angle">알아채는 각도</param>
        /// <param name="patienceSeconds">참는 시간</param>
        /// <param name="blame">사람 탓으로 볼 거리</param>
        public static Response Judge(Sense sense, float radius, float angle,
                                     float patienceSeconds, float blame)
        {
            // 맞았으면 보든 안 보든 항의합니다. 뒤에서 받히는 일이 흔합니다.
            if (sense.SinceHit < 0f) return Response.Ignore;

            bool near = sense.Distance <= radius && sense.Angle <= angle * 0.5f && sense.InSight;

            // <b>일을 못 하게 됐는가.</b> 갈 곳이 있는데 못 가고 있고, 그 앞에 사람이
            // 있을 때만 사람 탓입니다 — 바위에 걸린 것을 사람에게 항의하면 안 됩니다.
            bool blocked = sense.Working
                           && sense.Stuck >= patienceSeconds
                           && sense.Distance <= blame;

            if (blocked) return Response.Protest;

            return near ? Response.Notice : Response.Ignore;
        }

        // --- Private Methods ---

        /// <summary>지금 상태를 잽니다.</summary>
        private Sense Look(Transform who, float dt)
        {
            Sense sense = new Sense
            {
                Distance = float.MaxValue,
                Angle = 180f,
                InSight = false,
                Working = driver != null && driver.HasDestination,
                SinceHit = sinceHit,
            };

            // 갈 곳이 있는데 안 가고 있으면 막힌 시간을 셉니다.
            if (sense.Working && driver.CurrentSpeed < stuckSpeed) stuck += dt;
            else stuck = 0f;

            sense.Stuck = stuck;

            if (who == null) return sense;

            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            Vector3 to = who.position - eye;

            sense.Distance = to.magnitude;
            sense.Angle = Vector3.Angle(transform.forward, new Vector3(to.x, 0f, to.z));

            // ⚠ <c>QueryTriggerInteraction.Ignore</c> 입니다. 이 게임의 상호작용 판정과
            // 소리 구역이 전부 트리거라, 켜 두면 <b>허공이 시야를 가립니다.</b>
            sense.InSight = sense.Distance < 0.01f
                            || !Physics.Raycast(eye, to.normalized, sense.Distance * 0.98f,
                                                sightMask, QueryTriggerInteraction.Ignore);

            return sense;
        }

        /// <summary>바뀐 반응을 밖에 알리고, 항의 중에는 길을 멈춥니다.</summary>
        private void Apply(Response want)
        {
            // 얻어맞은 기억이 남아 있으면 알아챔으로 내려가지 않습니다.
            if (want != Response.Protest && sinceHit < grudgeSeconds) want = Response.Protest;

            // ⚠ <b>항의하는 동안 길을 멈춥니다.</b> 안 멈추면 항의하면서 걸어가
            // "비켜 달라" 가 "밀고 지나간다" 가 됩니다.
            if (patrol != null) patrol.Paused = want == Response.Protest;

            if (want == State) return;

            Response was = State;
            State = want;

            if (want == Response.Notice && onNoticed != null) onNoticed.Invoke();
            else if (want == Response.Protest && onProtested != null) onProtested.Invoke();
            else if (want == Response.Ignore && was != Response.Ignore && onCalmed != null)
            {
                onCalmed.Invoke();
            }
        }

        /// <summary>지금 볼 사람입니다. 차에 타고 있으면 차입니다.</summary>
        private Transform Player()
        {
            if (player != null) return player;

            GameObject found = GameObject.FindWithTag("Player");
            if (found != null) player = found.transform;

            return player;
        }
    }
}
