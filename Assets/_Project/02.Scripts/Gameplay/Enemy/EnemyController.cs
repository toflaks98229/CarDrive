using UnityEngine;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 차량을 XZ 평면(높이 무시)에서 쫓아오는 적입니다.
    ///
    /// 체력·피격 연출·사망 처리는 <see cref="EnemyBase"/>가 갖고 있습니다.
    /// 이 클래스에 남은 것은 <b>이 적만의 것</b> 셋뿐입니다. 추적, 충돌 판정, 사운드 연결.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class EnemyController : EnemyBase
    {
        // --- Public Member Variables ---

        /// <summary>추적할 대상입니다. 비워두면 노릴 차량을 스스로 찾습니다.</summary>
        [Header("추적 설정")]
        [Tooltip("추적할 대상의 Transform (비워두면 노릴 차량을 스스로 찾습니다)")]
        public Transform target;

        /// <summary>추적 이동 속도입니다.</summary>
        [Tooltip("적의 이동 속도")]
        public float moveSpeed = 3f;

        // 추적 대상과 충돌 판정은 태그 문자열이 아니라 Vehicle 컴포넌트로 합니다.
        // 태그는 오타를 컴파일러가 잡아 주지 못하고, 차가 두 대가 되면 의미가 모호해집니다.

        /// <summary>도보 플레이어와 부딪혔을 때 전달할 충격 세기 배율입니다.</summary>
        [Header("충돌 설정")]
        [Tooltip("도보 플레이어와 부딪혔을 때 전달할 충격 세기 배율")]
        public float impactScaleOnPlayer = 1f;

        /// <summary>도보 플레이어와 부딪혔을 때도 이 적이 사라질지 여부입니다.</summary>
        [Tooltip("도보 플레이어와 부딪혔을 때도 이 적이 사라질지 여부")]
        public bool dieOnFootPlayerHit = false;

        /// <summary>사운드를 재생할 컨트롤러입니다. 비워두면 같은 오브젝트에서 찾습니다.</summary>
        [Header("사운드")]
        [Tooltip("사운드를 재생할 컨트롤러. 비워두면 같은 오브젝트에서 찾습니다.")]
        public EnemySoundController soundController;

        // --- Private Member Variables ---

        /// <summary>이 적의 Rigidbody입니다. 속도를 직접 설정해 추적합니다.</summary>
        private Rigidbody rb;

        /// <summary>다음에 타겟을 다시 찾을 시각입니다. 매 물리 프레임 찾지 않으려고 둡니다.</summary>
        private float nextTargetSearchTime;

        /// <summary>
        /// 인스펙터에서 지정해 둔 추적 대상입니다.
        /// 풀에서 다시 꺼낼 때 지난번에 쫓던 차가 남아 있으면 안 되지만,
        /// 손으로 지정한 대상까지 지워 버리면 그것대로 규칙이 바뀝니다. 그래서 기억해 둡니다.
        /// </summary>
        private Transform authoredTarget;

        // --- Constants ---

        /// <summary>타겟을 잃었을 때 다시 찾는 간격(초)입니다.</summary>
        private const float TargetSearchInterval = 0.5f;

        // --- Unity Event Functions ---

        /// <summary>
        /// 물리 설정을 잡고 사운드 컨트롤러와 추적 대상을 찾습니다.
        /// 체력·연출 준비는 기반 클래스가 합니다.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();

            // 인스펙터 지정을 기억해 둡니다. 풀에서 재사용될 때 여기로 되돌립니다.
            authoredTarget = target;

            rb = GetComponent<Rigidbody>();
            rb.useGravity = true;
            rb.isKinematic = false;

            // 사운드는 있으면 쓰고 없으면 조용히 넘어갑니다.
            if (soundController == null) soundController = GetComponent<EnemySoundController>();
        }

        /// <summary>
        /// 타겟을 향해 XZ 평면으로 이동합니다. Y축 속도는 중력에 맡깁니다.
        /// </summary>
        void FixedUpdate()
        {
            if (target == null)
            {
                // 매 물리 프레임 찾지 않습니다. 차가 갑자기 생기는 일은 드뭅니다.
                if (Time.time >= nextTargetSearchTime)
                {
                    nextTargetSearchTime = Time.time + TargetSearchInterval;
                    ResolveTarget();
                }

                if (target == null)
                {
                    // 노릴 것이 없으면 XZ 이동을 멈춥니다. (Y축은 중력을 위해 유지)
                    rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
                    return;
                }
            }

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.0001f) return;

            Vector3 direction = toTarget.normalized;
            rb.linearVelocity = new Vector3(direction.x * moveSpeed, rb.linearVelocity.y, direction.z * moveSpeed);

            // 이동하는 방향을 바라보게 합니다. 높이는 자기 것을 유지합니다.
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        /// <summary>
        /// 도보 플레이어에게는 충격을 주고, 차량에 부딪히면 사라집니다.
        /// </summary>
        /// <param name="collision">충돌 정보</param>
        void OnCollisionEnter(Collision collision)
        {
            if (collision.collider == null) return;

            // 1. 도보 플레이어와 부딪혔는지 먼저 확인합니다.
            //    태그가 아니라 컴포넌트로 판정합니다. (PlayerCar가 이미 "Player" 태그를 쓰고 있음)
            PlayerImpactReceiver receiver = collision.collider.GetComponentInParent<PlayerImpactReceiver>();
            if (receiver != null)
            {
                Vector3 direction = receiver.transform.position - transform.position;
                receiver.TakeImpact(impactScaleOnPlayer, direction);

                if (dieOnFootPlayerHit)
                {
                    Debug.Log("EnemyController: 도보 플레이어와 충돌해 쓰러집니다.");
                    Die();
                }
                return;
            }

            // 2. 차량과 충돌하면 사라집니다.
            //    콜라이더가 자식(휠·차체)에 있을 수 있으므로 부모까지 올라가며 찾습니다.
            if (collision.collider.GetComponentInParent<Vehicle>() != null)
            {
                Debug.Log("EnemyController: 차량과 충돌해 쓰러집니다.");
                Die();
            }
        }

        // --- Protected Methods : EnemyBase 구현 ---

        /// <summary>피격음을 재생합니다.</summary>
        protected override void PlayDamageSound()
        {
            if (soundController != null) soundController.PlayTakeDamageSound();
        }

        /// <summary>사망음을 재생합니다. 위치 기반이라 오브젝트가 사라져도 들립니다.</summary>
        protected override void PlayDeathSound()
        {
            if (soundController != null) soundController.PlayDeathSound();
        }

        /// <summary>
        /// 풀로 돌아갑니다. 풀에서 나온 것이 아니면 예전처럼 파괴됩니다.
        /// (그 판단은 <see cref="PrefabPool.Release"/>가 합니다)
        /// </summary>
        protected override void Despawn()
        {
            PrefabPool.Release(gameObject);
        }

        /// <summary>
        /// 풀에서 다시 꺼내질 때 추적 상태도 새것으로 되돌립니다.
        /// </summary>
        protected override void ResetForSpawn()
        {
            base.ResetForSpawn();

            // 지난번에 쫓던 차가 그대로 남아 있으면 엉뚱한 곳으로 달려갑니다.
            // 인스펙터에서 지정한 대상이 있으면 그것으로, 없으면 비워 두고 다시 찾습니다.
            target = authoredTarget;
            nextTargetSearchTime = 0f;
        }

        // --- Private Methods ---

        /// <summary>
        /// 노릴 차량을 정합니다. 플레이어가 타고 있는 차가 있으면 그 차를,
        /// 없으면 가장 가까운 차를 고릅니다.
        /// </summary>
        private void ResolveTarget()
        {
            Vehicle prey = Vehicle.GetTargetVehicle(transform.position);
            target = prey != null ? prey.transform : null;
        }
    }
}
