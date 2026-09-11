using UnityEngine;
using UnityEngine.Events;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 죽은 차를 <b>가지러 오는</b> 기계입니다.
    ///
    /// <b>메우는 구멍.</b> 연료가 0 이 되고 내구도가 0 이 되면 <b>그다음이 없었습니다</b>
    /// (<c>로봇_기획.md</c> 의 5번). 길 한복판에서 차가 멈추면 남는 선택은 불러오기뿐이고,
    /// 그러면 실패가 <b>사건이 아니라 되감기</b>가 됩니다.
    ///
    /// 가지러 오는 기계가 있으면 실패에 <b>뒤</b>가 생깁니다 — 기다리고, 값을 치르고,
    /// 마을로 끌려가고, 거기서 <see cref="RepairArm"/> 에 다시 값을 치릅니다.
    /// 돈이 처음으로 <b>아픈 곳</b>에 쓰이는 자리가 여기입니다.
    ///
    /// ⚠ <b>거절하지 않습니다.</b> 돈이 모자라면 가진 것을 다 가져가고 그래도 끌고 갑니다.
    /// 거절하면 플레이어가 길 한복판에 <b>영영</b> 남고, 그때는 불러오기 말고 길이 없어
    /// 이 기계를 만든 이유가 사라집니다. 기계는 흥정하지 않습니다.
    ///
    /// ⚠ <b>말을 걸지 않습니다.</b> 값은 지갑에서 빠져나가는 것으로 알립니다.
    /// 이 세계에 남은 사람은 적고, 기계는 사람이 아닙니다.
    /// </summary>
    [AddComponentMenu("CarDrive/견인기 (TowRig)")]
    public class TowRig : MonoBehaviour
    {
        // --- Public Types ---

        /// <summary>지금 무엇을 하는 중인가.</summary>
        public enum Phase
        {
            /// <summary>제자리에서 기다립니다.</summary>
            Waiting,

            /// <summary>죽은 차를 향해 걸어갑니다.</summary>
            Coming,

            /// <summary>갈고리를 내리고 값을 받습니다.</summary>
            Hooking,

            /// <summary>마을로 끌고 갑니다.</summary>
            Hauling,

            /// <summary>내려놓고 제자리로 돌아갑니다.</summary>
            Leaving,
        }

        // --- Public Member Variables ---

        /// <summary>걸어 주는 부품입니다. 비워두면 자기 계층에서 찾습니다.</summary>
        [Header("부품")]
        [Tooltip("걸어 주는 부품. 비워두면 자기 계층에서 찾습니다")]
        public RobotDriver driver;

        /// <summary>갈고리입니다. 있으면 걸 때 내리고 놓을 때 올립니다.</summary>
        [Tooltip("갈고리. 있으면 걸 때 내리고 놓을 때 올립니다")]
        public WeaponWinch winch;

        /// <summary>끌고 갈 곳입니다. 보통 마을의 정비 팔 옆입니다.</summary>
        [Header("어디까지")]
        [Tooltip("끌고 갈 곳. 보통 마을의 정비 팔 옆입니다")]
        public Transform yard;

        /// <summary>이 안에서 멈춘 차를 알아챕니다(m).</summary>
        [Tooltip("이 안에서 멈춘 차를 알아챕니다(m)")]
        [Range(10f, 600f)]
        public float notice = 400f;

        /// <summary>이만큼 다가가야 갈고리가 닿습니다(m).</summary>
        [Tooltip("이만큼 다가가야 갈고리가 닿습니다(m)")]
        [Range(2f, 15f)]
        public float hookRange = 5f;

        /// <summary>
        /// 손을 대기만 해도 받는 값입니다.
        ///
        /// 먼 길을 끌고 오는 것이 이 기계의 일이므로, 거리 값이 주이고 이것은 하한입니다.
        /// </summary>
        [Header("값")]
        [Tooltip("손을 대기만 해도 받는 값")]
        [Range(0, 300)]
        public int callOut = 40;

        /// <summary>100 m 를 끌 때마다 받는 값입니다.</summary>
        [Tooltip("100 m 를 끌 때마다 받는 값")]
        [Range(0, 100)]
        public int pricePerHundredMetres = 6;

        /// <summary>갈고리를 거는 데 걸리는 시간(초)입니다.</summary>
        [Header("일하는 모습")]
        [Tooltip("갈고리를 거는 데 걸리는 시간(초)")]
        [Range(0f, 15f)]
        public float hookSeconds = 3f;

        /// <summary>끌려오는 차가 걷는 기계 뒤로 떨어지는 거리(m)입니다.</summary>
        [Tooltip("끌려오는 차가 뒤로 떨어지는 거리(m)")]
        [Range(2f, 12f)]
        public float towDistance = 5f;

        /// <summary>갈고리가 차에 걸릴 때 낼 소리입니다.</summary>
        [Header("소리")]
        [Tooltip("갈고리가 차에 걸릴 때 낼 소리")]
        public AudioClip hookSound;

        /// <summary>차를 내려놓을 때 낼 소리입니다.</summary>
        [Tooltip("차를 내려놓을 때 낼 소리")]
        public AudioClip dropSound;

        /// <summary>소리 크기입니다.</summary>
        [Tooltip("소리 크기")]
        [Range(0f, 1f)]
        public float volume = 0.7f;

        /// <summary>차를 걸었을 때 부릅니다.</summary>
        [Header("알림")]
        public UnityEvent onHooked;

        /// <summary>마을에 내려놓았을 때 부릅니다.</summary>
        public UnityEvent onDelivered;

        // --- Public Properties ---

        /// <summary>지금 하는 일입니다.</summary>
        public Phase Doing { get; private set; }

        /// <summary>지금 끌고 있는 차입니다. 없으면 null 입니다.</summary>
        public Vehicle Hauled { get; private set; }

        /// <summary>마지막으로 받은 값입니다.</summary>
        public int LastBill { get; private set; }

        // --- Private Member Variables ---

        private Wallet wallet;
        private Vector3 post;
        private float working;
        private bool wasKinematic;

        // --- Unity Event Functions ---

        void Awake()
        {
            if (driver == null) driver = GetComponentInChildren<RobotDriver>(true);
            if (winch == null) winch = GetComponentInChildren<WeaponWinch>(true);

            post = transform.position;
        }

        void Start()
        {
            wallet = GameContext.Resolve<Wallet>(this);
        }

        void Update()
        {
            switch (Doing)
            {
                case Phase.Waiting: Look(); break;
                case Phase.Coming: Walk(); break;
                case Phase.Hooking: Hook(Time.deltaTime); break;
                case Phase.Hauling: Haul(); break;
                case Phase.Leaving: Leave(); break;
            }

            // 끌고 가는 동안에는 차가 뒤를 따라옵니다.
            if (Doing == Phase.Hauling && Hauled != null) Drag();
        }

        // --- Public Methods ---

        /// <summary>
        /// 그 차가 <b>멈춰 선</b> 차인가.
        ///
        /// <b>왜 갈라 두는가.</b> 이 판단 하나가 기계를 부릅니다. 차도 지갑도 없이
        /// 확인할 수 있어야 합니다.
        /// </summary>
        /// <param name="health">남은 내구도</param>
        /// <param name="fuel">남은 연료</param>
        /// <returns>내구도나 연료가 바닥났으면 true</returns>
        public static bool Stranded(float health, float fuel)
        {
            return health <= 0f || fuel <= 0f;
        }

        /// <summary>
        /// 받을 값입니다.
        ///
        /// ⚠ <b>거리로 받습니다.</b> 정액이면 마을 앞에서 멈추나 세계 끝에서 멈추나
        /// 같은 값이 되어, 멀리 나가는 일에 값이 매겨지지 않습니다. 멀리 나가는 것이
        /// 이 게임의 내용이므로 그 값은 거리에 붙어야 합니다.
        /// </summary>
        /// <param name="metres">끌고 갈 거리(m)</param>
        /// <param name="perHundredMetres">100 m 당 값</param>
        /// <param name="baseFee">손을 대기만 해도 받는 값</param>
        /// <returns>받을 값</returns>
        public static int Quote(float metres, int perHundredMetres, int baseFee)
        {
            float far = Mathf.Max(metres, 0f);

            return baseFee + Mathf.CeilToInt(far / 100f * perHundredMetres);
        }

        /// <summary>그 차를 지금 부르러 갈 수 있는가.</summary>
        /// <param name="car">볼 차</param>
        /// <returns>멈춰 섰고 알아챌 거리 안이면 true</returns>
        public bool Wants(Vehicle car)
        {
            if (car == null || Hauled != null) return false;

            float health = car.health != null ? car.health.CurrentHealth : 1f;

            if (!Stranded(health, Fuel(car))) return false;

            return Vector3.Distance(car.transform.position, transform.position) <= notice;
        }

        // --- Private Methods ---

        /// <summary>
        /// 멈춰 선 차를 찾습니다.
        ///
        /// ⚠ <b>가장 가까운 차가 아니라 가장 가까운 <b>멈춰 선</b> 차입니다.</b>
        /// 처음에는 <c>Vehicle.FindNearest</c> 하나로 봤는데, 마을에 세워 둔 멀쩡한
        /// 차가 늘 더 가까워서 <b>길에 선 차를 영영 못 봤습니다.</b>
        /// </summary>
        private void Look()
        {
            Vehicle best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < Vehicle.All.Count; i++)
            {
                Vehicle car = Vehicle.All[i];
                if (!Wants(car)) continue;

                float sqr = (car.transform.position - transform.position).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = car;
            }

            if (best == null) return;

            Hauled = best;
            Doing = Phase.Coming;
        }

        /// <summary>차를 향해 걸어갑니다.</summary>
        private void Walk()
        {
            if (Hauled == null) { Doing = Phase.Leaving; return; }

            Vector3 to = Hauled.transform.position;
            if (driver != null) driver.SetDestination(to);

            if (Flat(to, transform.position) > hookRange) return;

            if (driver != null) driver.ClearDestination();
            if (winch != null) winch.Lower();

            working = hookSeconds;
            Doing = Phase.Hooking;
        }

        /// <summary>갈고리를 걸고 값을 받습니다.</summary>
        /// <param name="dt">지난 시간(초)</param>
        private void Hook(float dt)
        {
            working -= dt;
            if (working > 0f) return;

            if (Hauled == null) { Doing = Phase.Leaving; return; }

            Vector3 to = yard != null ? yard.position : post;
            LastBill = Quote(Flat(to, Hauled.transform.position), pricePerHundredMetres, callOut);

            Pay(LastBill);
            Grab();

            Say(hookSound, Hauled.transform.position);
            if (onHooked != null) onHooked.Invoke();
            Doing = Phase.Hauling;
        }

        /// <summary>마을로 끌고 갑니다.</summary>
        private void Haul()
        {
            if (Hauled == null) { Doing = Phase.Leaving; return; }

            Vector3 to = yard != null ? yard.position : post;
            if (driver != null) driver.SetDestination(to);

            if (Flat(to, transform.position) > hookRange) return;

            Vector3 where = Hauled.transform.position;
            Drop();

            if (winch != null) winch.Raise();

            Say(dropSound, where);
            if (onDelivered != null) onDelivered.Invoke();

            Doing = Phase.Leaving;
        }

        /// <summary>제자리로 돌아갑니다.</summary>
        private void Leave()
        {
            if (driver != null) driver.SetDestination(post);

            if (Flat(post, transform.position) > hookRange) return;

            if (driver != null) driver.ClearDestination();
            Doing = Phase.Waiting;
        }

        /// <summary>
        /// 값을 받습니다.
        ///
        /// ⚠ <b>모자라면 가진 것을 다 가져갑니다.</b> 거절하면 플레이어가 길에 영영
        /// 남습니다. 맨 위 주석의 이유입니다.
        /// </summary>
        /// <param name="price">받을 값</param>
        private void Pay(int price)
        {
            if (wallet == null || price <= 0) return;

            if (wallet.TrySpend(CurrencyType.Money, price)) return;

            int had = wallet.Get(CurrencyType.Money);
            if (had > 0) wallet.TrySpend(CurrencyType.Money, had);

            LastBill = had;
        }

        /// <summary>
        /// 차를 겁니다.
        ///
        /// ⚠ <b>물리를 재웁니다.</b> 안 재우면 끌려오는 차가 바퀴로 버티며 튕깁니다.
        /// 원래 상태를 적어 두었다가 내려놓을 때 그대로 돌려 놓습니다.
        /// </summary>
        private void Grab()
        {
            Rigidbody body = Hauled.GetComponent<Rigidbody>();
            if (body == null) return;

            wasKinematic = body.isKinematic;
            body.isKinematic = true;
        }

        /// <summary>차를 내려놓습니다.</summary>
        private void Drop()
        {
            Rigidbody body = Hauled.GetComponent<Rigidbody>();
            if (body != null) body.isKinematic = wasKinematic;

            Hauled = null;
        }

        /// <summary>끌려오는 차를 뒤에 붙여 둡니다.</summary>
        private void Drag()
        {
            Transform rope = winch != null && winch.hook != null ? winch.hook : transform;

            Vector3 behind = transform.position - transform.forward * towDistance;
            behind.y = rope.position.y;

            Hauled.transform.position = behind;
            Hauled.transform.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
        }

        /// <summary>
        /// 그 차의 남은 연료입니다.
        ///
        /// ⚠ <b>연료통이 없는 차는 기름으로 서지 않습니다.</b> 동력계가 없거나
        /// 용량이 0 이면 이 기계가 볼 수치가 아닙니다. 그렇게 두지 않으면 용량이
        /// 안 잡힌 차가 <b>늘 빈 차</b>로 보여 기계가 끝없이 끌러 옵니다.
        /// </summary>
        /// <param name="car">볼 차</param>
        /// <returns>남은 연료. 볼 수치가 없으면 1</returns>
        private static float Fuel(Vehicle car)
        {
            Powertrain engine = car.controller != null
                                ? car.controller.GetComponent<Powertrain>() : null;

            if (engine == null || engine.MaxFuel <= 0f) return 1f;

            return engine.CurrentFuel;
        }

        /// <summary>소리를 한 번 냅니다.</summary>
        /// <param name="clip">낼 소리. 없으면 아무 일도 하지 않습니다</param>
        /// <param name="at">소리가 날 자리</param>
        private void Say(AudioClip clip, Vector3 at)
        {
            if (clip == null) return;

            OneShotAudioPool.Play(clip, at, volume);
        }

        /// <summary>높이를 뺀 거리입니다. 로봇은 언덕 위에 있을 수 있습니다.</summary>
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
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, notice);

            if (yard == null) return;

            Gizmos.color = new Color(0.6f, 1f, 0.6f, 0.8f);
            Gizmos.DrawLine(transform.position, yard.position);
        }
    }
}
