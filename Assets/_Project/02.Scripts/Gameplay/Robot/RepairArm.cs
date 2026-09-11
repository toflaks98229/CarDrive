using UnityEngine;
using UnityEngine.Events;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 돈을 받고 차를 고쳐 주는 <b>붙박이 기계</b>입니다.
    ///
    /// <b>메우는 구멍.</b> 귀신이 내구도를 깎는데 <b>회복 경로가 하나도 없었습니다</b>
    /// (<c>로봇_기획.md</c> 의 "더 필요한 것 셋"). 깎이기만 하고 돌아올 길이 없으면
    /// 그 수치는 <b>시계</b>일 뿐 자원이 아닙니다.
    ///
    /// <b>마을이 거점이 되는 이유이기도 합니다.</b> 지금 마을에는 상점과 침대가 있지만
    /// <b>돌아갈 이유</b>가 없었습니다. 고쳐 주는 기계가 있으면 돌아가는 일에 뜻이
    /// 생기고, 돈이 처음으로 <b>아픈 곳</b>에 쓰입니다.
    ///
    /// <b>다리가 없습니다.</b> 이것은 보행기가 아니라 받침대에 올린 팔입니다 —
    /// 기획이 "만드는 비용 낮음, 보행 리그 불필요" 라고 적어 둔 그대로입니다.
    /// 움직이는 것은 <see cref="RobotTurret"/> 한 단뿐이고, 그것도 <b>있으면</b> 씁니다.
    ///
    /// ⚠ <b>말을 걸지 않습니다.</b> 이 세계에 남은 사람은 적고, 기계는 사람이 아닙니다.
    /// 값은 표시되고 <c>E</c> 로 지불됩니다. 그것이 이 기계가 하는 말 전부입니다.
    /// </summary>
    [AddComponentMenu("CarDrive/정비 팔 (RepairArm)")]
    public class RepairArm : MonoBehaviour, IInteractable
    {
        // --- Public Member Variables ---

        /// <summary>차가 이 안에 있어야 손이 닿습니다(m).</summary>
        [Header("닿는 거리")]
        [Tooltip("차가 이 안에 있어야 고쳐 줍니다(m)")]
        [Range(2f, 30f)]
        public float reach = 9f;

        /// <summary>
        /// 내구도 1 을 되돌리는 값입니다.
        ///
        /// ⚠ <b>고칠 것이 많을수록 비쌉니다.</b> 정액이면 다 부서질 때까지 기다리는
        /// 것이 이득이 되어, 조심해서 모는 쪽이 손해를 봅니다.
        /// </summary>
        [Header("값")]
        [Tooltip("내구도 1 을 되돌리는 값")]
        [Range(1, 20)]
        public int pricePerPoint = 3;

        /// <summary>연료 1 을 채우는 값입니다.</summary>
        [Tooltip("연료 1 을 채우는 값. 0 이면 주유를 안 합니다")]
        [Range(0, 20)]
        public int pricePerFuel = 2;

        /// <summary>
        /// 손을 대기만 해도 받는 값입니다.
        ///
        /// 긁힌 자국 하나에 1 원을 받으면 기계가 아니라 <b>자판기</b>처럼 보입니다.
        /// </summary>
        [Tooltip("손을 대기만 해도 받는 값")]
        [Range(0, 200)]
        public int callOut = 15;

        /// <summary>고치는 데 걸리는 시간(초)입니다.</summary>
        [Header("일하는 모습")]
        [Tooltip("고치는 데 걸리는 시간(초). 그동안 팔이 차를 향합니다")]
        [Range(0f, 15f)]
        public float workSeconds = 3.5f;

        /// <summary>있으면 일하는 동안 차를 향해 굽힙니다.</summary>
        [Tooltip("있으면 일하는 동안 차를 향해 굽힙니다")]
        public RobotTurret arm;

        /// <summary>손을 대기 시작할 때 낼 소리입니다.</summary>
        [Header("소리")]
        [Tooltip("손을 대기 시작할 때 낼 소리")]
        public AudioClip startSound;

        /// <summary>다 고쳤을 때 낼 소리입니다.</summary>
        [Tooltip("다 고쳤을 때 낼 소리")]
        public AudioClip doneSound;

        /// <summary>돈이 모자랄 때 낼 소리입니다.</summary>
        [Tooltip("돈이 모자랄 때 낼 소리")]
        public AudioClip refusedSound;

        /// <summary>소리 크기입니다.</summary>
        [Tooltip("소리 크기")]
        [Range(0f, 1f)]
        public float volume = 0.6f;

        /// <summary>일을 시작할 때 부릅니다.</summary>
        [Header("알림")]
        public UnityEvent onStarted;

        /// <summary>다 고쳤을 때 부릅니다.</summary>
        public UnityEvent onFinished;

        /// <summary>돈이 모자랄 때 부릅니다.</summary>
        public UnityEvent onRefused;

        // --- Public Properties ---

        /// <summary>지금 고치는 중인지 여부입니다.</summary>
        public bool Working { get; private set; }

        // --- Private Member Variables ---

        private Wallet wallet;
        private float working;
        private Vehicle busyWith;

        // --- Unity Event Functions ---

        void Start()
        {
            wallet = GameContext.Resolve<Wallet>(this);
        }

        void Update()
        {
            if (!Working) return;

            working -= Time.deltaTime;

            // 일하는 동안 팔이 차를 봅니다. 다 하면 제자리로 돌아갑니다.
            if (arm != null && busyWith != null) arm.AimAt(busyWith.transform.position);

            if (working > 0f) return;

            Working = false;
            Mend(busyWith);
            busyWith = null;

            if (arm != null) arm.StopAiming();

            Say(doneSound);
            if (onFinished != null) onFinished.Invoke();
        }

        // --- Public Methods ---

        /// <summary>
        /// 고칠 것이 있고 차가 닿는 자리에 있는가.
        ///
        /// ⚠ <b>멀쩡한 차에는 안 걸립니다.</b> 걸리게 두면 조준점이 이 기계에 닿을 때마다
        /// "수리 0 원" 이 떠서, 정작 고쳐야 할 때 그 표시를 안 믿게 됩니다.
        /// </summary>
        public bool CanInteract()
        {
            if (Working) return false;

            Vehicle car = Nearest();
            return car != null && Bill(car) > 0;
        }

        /// <summary>화면에 띄울 말입니다.</summary>
        public string GetInteractionLabel()
        {
            Vehicle car = Nearest();
            if (car == null) return "";

            int price = Bill(car);
            return price > 0 ? "정비 (" + price + ")" : "";
        }

        /// <summary>값을 치르고 일을 시작합니다.</summary>
        public void Interact()
        {
            if (Working) return;

            Vehicle car = Nearest();
            if (car == null) return;

            int price = Bill(car);
            if (price <= 0) return;

            // ⚠ <b>먼저 받고 고칩니다.</b> 고치고 받으면 돈이 모자랄 때 <b>공짜로
            // 고쳐 준 뒤</b> 거절하게 됩니다.
            if (wallet == null || !wallet.TrySpend(CurrencyType.Money, price))
            {
                Say(refusedSound);
                if (onRefused != null) onRefused.Invoke();
                return;
            }

            busyWith = car;
            Working = true;
            working = workSeconds;

            Say(startSound);
            if (onStarted != null) onStarted.Invoke();

            // 시간이 0 이면 그 자리에서 끝냅니다.
            if (workSeconds <= 0f)
            {
                Working = false;
                Mend(car);
                busyWith = null;

                Say(doneSound);
                if (onFinished != null) onFinished.Invoke();
            }
        }

        /// <summary>
        /// 받을 값입니다.
        ///
        /// <b>왜 갈라 두는가.</b> 값 매기는 규칙이 이 기계의 유일한 판단입니다.
        /// 차도 지갑도 없이 확인할 수 있어야 합니다.
        /// </summary>
        /// <param name="missingHealth">깎여 있는 내구도</param>
        /// <param name="missingFuel">비어 있는 연료</param>
        /// <param name="perPoint">내구도 1 당 값</param>
        /// <param name="perFuel">연료 1 당 값</param>
        /// <param name="baseFee">손을 대기만 해도 받는 값</param>
        /// <returns>받을 값. 고칠 것이 없으면 0</returns>
        public static int Quote(float missingHealth, float missingFuel,
                                int perPoint, int perFuel, int baseFee)
        {
            int forHealth = Mathf.CeilToInt(Mathf.Max(missingHealth, 0f)) * perPoint;
            int forFuel = Mathf.CeilToInt(Mathf.Max(missingFuel, 0f)) * perFuel;

            // ⚠ <b>고칠 것이 없으면 출장비도 없습니다.</b> 안 그러면 멀쩡한 차에
            // 출장비만 받는 기계가 됩니다.
            if (forHealth + forFuel <= 0) return 0;

            return baseFee + forHealth + forFuel;
        }

        // --- Private Methods ---

        /// <summary>
        /// 소리를 한 번 냅니다.
        ///
        /// <b>이 기계는 말을 하지 않습니다.</b> 손을 대는 소리, 다 된 소리, 거절하는
        /// 소리 셋뿐이고 그것이 이 기계가 하는 말 전부입니다.
        /// </summary>
        /// <param name="clip">낼 소리. 없으면 아무 일도 하지 않습니다</param>
        private void Say(AudioClip clip)
        {
            if (clip == null) return;

            OneShotAudioPool.Play(clip, transform.position, volume);
        }

        /// <summary>이 차에 매길 값입니다.</summary>
        private int Bill(Vehicle car)
        {
            float hurt = car.health != null
                         ? car.health.maxHealth - car.health.CurrentHealth : 0f;

            float dry = Dry(car);

            return Quote(hurt, dry, pricePerPoint, pricePerFuel, callOut);
        }

        /// <summary>비어 있는 연료입니다. 주유를 안 하면 0 입니다.</summary>
        private float Dry(Vehicle car)
        {
            if (pricePerFuel <= 0) return 0f;

            Powertrain engine = car.controller != null
                                ? car.controller.GetComponent<Powertrain>() : null;
            if (engine == null) return 0f;

            return Mathf.Max(engine.MaxFuel - engine.CurrentFuel, 0f);
        }

        /// <summary>실제로 고칩니다.</summary>
        private void Mend(Vehicle car)
        {
            if (car == null) return;

            if (car.health != null)
            {
                car.health.Heal(car.health.maxHealth - car.health.CurrentHealth);
            }

            if (pricePerFuel <= 0) return;

            Powertrain engine = car.controller != null
                                ? car.controller.GetComponent<Powertrain>() : null;
            if (engine != null) engine.SetFuel(engine.MaxFuel);
        }

        /// <summary>닿는 자리에 있는 차입니다.</summary>
        private Vehicle Nearest()
        {
            Vehicle car = Vehicle.FindNearest(transform.position);
            if (car == null) return null;

            return Vector3.Distance(car.transform.position, transform.position) <= reach
                   ? car : null;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.5f, 1f, 0.6f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, reach);
        }
    }
}
