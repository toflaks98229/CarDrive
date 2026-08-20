using UnityEngine;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 차량에 달라붙어 로컬 좌표로 다가온 뒤 주기적으로 내구도를 깎는 귀신입니다.
    /// Rigidbody를 쓰지 않고 부모(차량)의 자식으로 움직입니다.
    ///
    /// 체력·피격 연출·사망 처리는 <see cref="EnemyBase"/>가 갖고 있습니다.
    /// 이 클래스에 남은 것은 <b>이 귀신만의 것</b>입니다. 접근, 부착, 주기 공격.
    /// </summary>
    public class AttachedGhostController : EnemyBase
    {
        // --- Public Member Variables ---

        /// <summary>부모(차량)의 로컬 좌표 기준 목표 지점까지 다가오는 속도입니다.</summary>
        [Header("귀신 이동 설정")]
        [Tooltip("부모(차량)의 로컬 좌표 기준 목표 지점까지 이동하는 속도")]
        public float moveSpeed = 1.0f;

        /// <summary>이 거리 안으로 들어오면 부착한 것으로 보고 공격을 시작합니다.</summary>
        [Tooltip("목표 지점과 이 거리만큼 가까워지면 공격을 시작합니다.")]
        public float attackDistance = 1.0f;

        /// <summary>공격 주기(초)입니다.</summary>
        [Header("귀신 공격 설정")]
        [Tooltip("공격 주기 (초)")]
        public float damageInterval = 1.0f;

        /// <summary>주기마다 차량 내구도에서 깎을 양입니다.</summary>
        [Tooltip("주기마다 입힐 데미지")]
        public int damageToDeal = 5;

        /// <summary>주기마다 올릴 스트레스입니다.</summary>
        [Tooltip("주기마다 올릴 스트레스 (NeedsSystem이 씬에 없으면 무시됩니다)")]
        public float stressPerAttack = 0.04f;

        /// <summary>주기마다 올릴 더러움입니다.</summary>
        [Tooltip("주기마다 올릴 더러움 (귀신이 남기는 흔적)")]
        public float hygieneCostPerAttack = 0.01f;

        /// <summary>주기마다 차체를 흔드는 세기입니다. 0이면 흔들지 않습니다.</summary>
        [Tooltip("주기마다 차체를 흔드는 세기. 0이면 흔들지 않습니다.")]
        public float carShakeScalePerAttack = 0.35f;

        /// <summary>사운드를 재생할 컨트롤러입니다. 비워두면 같은 오브젝트에서 찾습니다.</summary>
        [Header("사운드")]
        [Tooltip("사운드를 재생할 컨트롤러. 비워두면 같은 오브젝트에서 찾습니다.")]
        public AttachedGhostSoundController soundController;

        // --- Public Member Variables : 코드로만 연결 ---

        /// <summary>
        /// 쓰러져 물러날 때 알립니다. 스폰한 쪽(GhostSpawner)이 자리를 비우고 풀로 돌려보냅니다.
        /// 연결되어 있지 않으면 스스로 풀로 돌아갑니다.
        /// </summary>
        public System.Action<AttachedGhostController> onDespawned;

        // --- Private Member Variables ---

        /// <summary>공격할 목표 지점입니다. 부모(차량) 기준 로컬 좌표입니다.</summary>
        private Vector3 targetLocalPosition;

        /// <summary>공격할 대상인 차량의 내구도입니다.</summary>
        private VehicleHealth carHealth;

        /// <summary>차체 흔들림입니다. 처음 필요할 때 찾아 캐시합니다.</summary>
        private CarImpactShake carImpactShake;

        /// <summary>다음 주기 공격까지 남은 시간(초)입니다.</summary>
        private float damageTimer;

        /// <summary>목표 지점에 도달해 차량에 달라붙었는지 여부입니다.</summary>
        private bool hasArrived;

        // --- Unity Event Functions ---

        /// <summary>
        /// 사운드 컨트롤러를 찾습니다. 체력·연출 준비는 기반 클래스가 합니다.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            // 사운드는 있으면 쓰고 없으면 조용히 넘어갑니다.
            if (soundController == null) soundController = GetComponent<AttachedGhostSoundController>();
        }

        /// <summary>
        /// 다가오는 중이면 이동하고, 이미 붙었으면 주기 공격을 처리합니다.
        /// </summary>
        void Update()
        {
            if (hasArrived) HandleProximityDamage();
            else HandleMovement();
        }

        // --- Public Methods ---

        /// <summary>
        /// GhostSpawner가 호출해 이 귀신을 초기화합니다.
        /// </summary>
        /// <param name="targetHealth">공격할 차량의 내구도</param>
        /// <param name="localTarget">달라붙을 목표 로컬 좌표</param>
        public void Initialize(VehicleHealth targetHealth, Vector3 localTarget)
        {
            carHealth = targetHealth;
            targetLocalPosition = localTarget;

            // 풀에서 꺼낼 때 OnEnable이 이미 상태를 되돌려 두지만,
            // 스폰한 쪽이 이 메서드만 보고 "새로 시작한다"고 믿을 수 있어야 합니다.
            ResetForSpawn();
        }

        // --- Protected Methods : EnemyBase 구현 ---

        /// <summary>피격음을 재생합니다.</summary>
        protected override void PlayDamageSound()
        {
            if (soundController != null) soundController.PlayTakeDamageSound();
        }

        /// <summary>사망음을 재생합니다. 루프를 끄고 위치 기반으로 재생합니다.</summary>
        protected override void PlayDeathSound()
        {
            if (soundController != null) soundController.PlayDeathSound();
        }

        /// <summary>
        /// 자리에서 물러납니다. 스폰한 쪽이 자리를 비워 줘야 다음 귀신이 나오므로
        /// 먼저 알리고, 연결되어 있지 않을 때만 스스로 풀로 돌아갑니다.
        /// </summary>
        protected override void Despawn()
        {
            if (onDespawned != null) onDespawned(this);
            else PrefabPool.Release(gameObject);
        }

        /// <summary>
        /// 풀에서 다시 꺼내질 때 접근·공격 상태도 새것으로 되돌립니다.
        /// 파괴되지 않으므로 필드가 초기화되지 않는다는 점을 반드시 염두에 두어야 합니다.
        /// </summary>
        protected override void ResetForSpawn()
        {
            base.ResetForSpawn();

            hasArrived = false;
            damageTimer = damageInterval;

            // 다른 차량에 붙을 수 있으므로 차체 흔들림 참조도 다시 찾게 합니다.
            carImpactShake = null;
        }

        // --- Private Methods ---

        /// <summary>
        /// 부모(차량) 기준 로컬 좌표로 목표 지점까지 다가옵니다.
        /// </summary>
        private void HandleMovement()
        {
            if (transform.parent == null) return;

            transform.localPosition = Vector3.MoveTowards(
                transform.localPosition,
                targetLocalPosition,
                moveSpeed * Time.deltaTime);

            // 도착했는지만 보면 되므로 제곱근을 구하지 않고 제곱끼리 비교합니다.
            float sqrToTarget = (targetLocalPosition - transform.localPosition).sqrMagnitude;
            if (sqrToTarget > attackDistance * attackDistance) return;

            hasArrived = true;
            Debug.Log(gameObject.name + "가 차량에 도착했습니다. 공격을 시작합니다.");

            // 부착되었으니 속삭임·공격 루프를 시작합니다.
            if (soundController != null) soundController.StartAttackLoop();
        }

        /// <summary>
        /// 달라붙은 동안 주기적으로 차량을 때립니다.
        /// </summary>
        private void HandleProximityDamage()
        {
            damageTimer -= Time.deltaTime;
            if (damageTimer > 0f) return;

            damageTimer = damageInterval;

            if (carHealth != null) carHealth.TakeDamage(damageToDeal);

            // 한 대 칠 때마다 타격음이 납니다.
            if (soundController != null) soundController.PlayAttackImpact();

            // 귀신에게 시달리면 스트레스와 더러움이 오릅니다.
            NeedsSystem.Report(NeedType.Stress, stressPerAttack);
            NeedsSystem.Report(NeedType.Hygiene, hygieneCostPerAttack);

            // 맞을 때마다 차체가 출렁입니다. (귀신은 차량의 자식으로 붙어 있습니다)
            if (carShakeScalePerAttack <= 0f) return;

            if (carImpactShake == null) carImpactShake = GetComponentInParent<CarImpactShake>();
            if (carImpactShake == null) return;

            Vector3 direction = carImpactShake.transform.position - transform.position;
            carImpactShake.TriggerImpactShake(direction, carShakeScalePerAttack);
        }
    }
}
