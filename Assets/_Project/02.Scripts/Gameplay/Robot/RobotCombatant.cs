using UnityEngine;
using UnityEngine.Events;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 보행 로봇을 <b>게임의 전투 규약에 연결</b>합니다.
    ///
    /// <b>왜 따로 있는가.</b> <see cref="WalkerRobot"/> 은 리그입니다 — 다리를 어디에 놓을지만 압니다.
    /// 체력과 적대는 다른 관심사이고, 그것을 리그에 얹으면 이 저장소에서 가장 큰 파일이 더 커집니다.
    /// 그래서 <b>붙이면 적이 되는 부품</b>으로 뒀습니다. 떼면 다시 배경 장치가 됩니다.
    ///
    /// <b>왜 <see cref="EnemyBase"/> 를 상속하지 않는가.</b> 그쪽은 귀신에 맞춰져 있습니다 —
    /// 렌더러 점멸, 사망 파티클, 재화 드롭, 그리고 <b>풀로 돌아가기</b>가 전제입니다.
    /// 보행 로봇은 풀에서 나오지 않고, 쓰러져도 사라지지 않고 <b>스스로 일어납니다</b>.
    /// 그 차이를 상속으로 덮으면 두 쪽 다 어색해집니다.
    ///
    /// <b>이 부품이 하는 일은 셋뿐입니다.</b>
    /// <code>
    /// 1. IHostile   — 차가 들이받고 발로 밟으면 적으로 인식된다   (표시만 하면 됩니다)
    /// 2. IDamageable — 앙크로 때리면 EnemyHealth 가 깎인다
    /// 3. 체력이 다하면 쓰러뜨리고, 다시 일어나지 못하게 한다
    /// </code>
    ///
    /// <b>1번은 코드가 필요 없습니다.</b> <see cref="CarCollisionHandler"/> 와
    /// <see cref="PlayerImpactReceiver"/> 는 이미 태그가 아니라 <see cref="IHostile"/> 로 적을
    /// 가리므로, 이 인터페이스를 다는 것만으로 충돌·피격 경로가 <b>한 줄도 고치지 않고</b> 열립니다.
    ///
    /// <b>맞은 방향은 받지 않습니다.</b> <see cref="IDamageable.TakeDamage"/> 에는 방향이 없기
    /// 때문입니다. 그래도 휘청임은 이미 나옵니다 — 차에 받히면 물리 충돌이
    /// <see cref="RobotPhysicsMotor.staggerOnCollision"/> 을 통해 방향까지 갖고 들어옵니다.
    /// 여기서 방향을 지어내면 <b>물리가 이미 만든 반응과 두 번 겹칩니다.</b>
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    [DisallowMultipleComponent]
    public class RobotCombatant : MonoBehaviour, IDamageable, IHostile
    {
        // --- Public Member Variables ---

        /// <summary>쓰러뜨릴 때 주는 충격의 세기입니다. <see cref="RobotKnockdown.knockdownThreshold"/> 를 넘겨야 실제로 넘어집니다.</summary>
        [Header("사망")]
        [Tooltip("체력이 다했을 때 쓰러뜨리는 충격의 세기. 넘어짐 문턱보다 커야 합니다.")]
        public float deathKnockdownStrength = 8f;

        /// <summary>
        /// 쓰러진 뒤 다시 일어나지 못하게 할지입니다.
        /// 끄면 체력이 0이어도 다시 일어납니다 — 무너뜨릴 수 없는 연출용 로봇에 씁니다.
        /// </summary>
        [Tooltip("체력이 다하면 다시 일어나지 못하게 합니다. 끄면 쓰러져도 다시 일어납니다.")]
        public bool stayDownOnDeath = true;

        /// <summary>피해를 입었을 때 호출됩니다. Feel 의 MMF_Player 를 여기에 연결하세요.</summary>
        [Header("이벤트")]
        [Tooltip("피해를 입었을 때. Feel 의 MMF_Player 를 여기에 연결하세요.")]
        public UnityEvent onDamaged;

        /// <summary>쓰러졌을 때 호출됩니다.</summary>
        [Tooltip("체력이 다해 쓰러졌을 때")]
        public UnityEvent onDied;

        // --- Public Properties : IDamageable ---

        /// <summary>체력이 다했는지입니다. 체력이 없으면 죽지 않는 것으로 봅니다.</summary>
        public bool IsDead { get { return health != null && health.IsDead; } }

        // --- Private Member Variables ---

        /// <summary>이 로봇의 체력입니다. <see cref="RequireComponent"/> 로 보장됩니다.</summary>
        private EnemyHealth health;

        /// <summary>넘어뜨리고 일으키는 쪽입니다. 없으면 쓰러지지 않고 그 자리에 섭니다.</summary>
        private RobotKnockdown knockdown;

        /// <summary>목적지를 향해 걷는 쪽입니다. 죽으면 걸음을 멈춥니다.</summary>
        private RobotDriver driver;

        /// <summary>무엇을 볼지 정하는 쪽입니다. 맞으면 깨웁니다.</summary>
        private RobotThreat threat;
        private RobotAwareness awareness;

        /// <summary>사망 처리를 이미 했는지입니다. 앙크는 매 프레임 때리므로 한 번만 돌아야 합니다.</summary>
        private bool died;

        // --- Unity Event Functions ---

        /// <summary>같은 계층에서 협력자를 찾습니다. 없어도 되는 것은 없는 대로 둡니다.</summary>
        void Awake()
        {
            health = GetComponent<EnemyHealth>();

            // 로봇은 부품이 루트에 모여 있지만, 프리팹 구조가 바뀌어도 견디도록 자식까지 봅니다.
            knockdown = GetComponentInChildren<RobotKnockdown>(true);
            driver = GetComponentInChildren<RobotDriver>(true);
            threat = GetComponentInChildren<RobotThreat>(true);
            awareness = GetComponentInParent<RobotAwareness>();
        }

        // --- Public Methods : IDamageable ---

        /// <summary>
        /// 피해를 입습니다. 체력을 깎고, 다하면 쓰러뜨립니다.
        ///
        /// <b>앙크는 매 프레임 이것을 부릅니다.</b> 그래서 사망 처리는 <see cref="died"/> 로
        /// 한 번만 돌게 막습니다. 그러지 않으면 쓰러뜨리는 충격이 프레임마다 들어가
        /// 로봇이 땅에 누운 채 계속 떨립니다.
        /// </summary>
        /// <param name="amount">입힐 피해량</param>
        public void TakeDamage(float amount)
        {
            if (health == null || health.IsDead) return;

            health.TakeDamage(amount);

            // <b>맞으면 깨어납니다.</b> 스트라이더는 건드려야만 겨누므로, 이 한 줄이
            // 없으면 앙크로 때려도 <b>가만히 서서 맞습니다.</b>
            if (threat != null) threat.Provoke();

            // 안 싸우는 기계도 맞으면 반응해야 합니다. 저쪽은 싸움을 열고
            // 이쪽은 서서 항의합니다 — 스트라이더에게는 이것이 전부입니다.
            if (awareness != null) awareness.Bumped();

            if (onDamaged != null) onDamaged.Invoke();

            if (health.IsDead) Die();
        }

        // --- Private Methods ---

        /// <summary>
        /// 쓰러뜨립니다. 걸음을 멈추고, 넘어뜨리고, 설정에 따라 그대로 눕혀 둡니다.
        ///
        /// <b>파괴하지 않습니다.</b> 이 로봇은 풀에서 나오지 않았고, 쓰러진 몸은
        /// <see cref="WalkerRobot"/> 이 <see cref="WalkerPosture.Limp"/> 로 계속 그려 줍니다.
        /// 사라지는 것보다 <b>그 자리에 남는 것</b>이 이 게임의 세계관에 맞습니다 —
        /// 지형이 파괴되지 않고 돌아갈 마을이 남아 있는 것과 같은 이유입니다.
        /// </summary>
        private void Die()
        {
            if (died) return;
            died = true;

            // 목적지를 지웁니다. 남겨 두면 쓰러진 채로 계속 그쪽으로 힘을 씁니다.
            if (driver != null)
            {
                driver.ClearDestination();
                driver.enabled = false;
            }

            if (knockdown != null)
            {
                // 다시 일어나지 못하게 하는 것이 먼저입니다.
                // 순서가 반대면 넘어지는 도중에 기상 판정이 한 번 돌 수 있습니다.
                if (stayDownOnDeath) knockdown.riseAutomatically = false;

                // 방향은 지금 향한 쪽의 반대입니다 — 맞은 방향을 모르므로,
                // 적어도 <b>가던 길로 고꾸라지는</b> 모습이 되게 합니다.
                knockdown.Knockdown(transform.forward, deathKnockdownStrength);
            }

            GameLog.Info(GameLog.Channel.Enemy, gameObject.name + ": 보행 로봇이 쓰러졌습니다.", this);

            if (onDied != null) onDied.Invoke();
        }
    }
}
