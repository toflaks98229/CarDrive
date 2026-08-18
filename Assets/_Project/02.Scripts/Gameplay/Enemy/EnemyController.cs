using UnityEngine;
using System.Collections; // [추가됨] 코루틴을 사용하기 위해 필요

/// <summary>
/// 타겟을 XZ 평면(높이 무시)에서 따라가고, 'Player' 태그와 충돌 시 또는 체력이 0이 되면 파괴되는 적 컨트롤러입니다.
/// 이 스크립트는 Rigidbody와 Collider 컴포넌트가 필요합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class EnemyController : MonoBehaviour, IDamageable, IHostile
{
    // --- Public Member Variables ---

    [Header("추적 설정")]
    [Tooltip("추적할 대상의 Transform (예: 플레이어 차량)")]
    public Transform target;

    [Tooltip("적의 이동 속도")]
    public float moveSpeed = 3f;

    [Tooltip("적의 최대 체력")]
    public float maxHealth = 100f;

    // 추적 대상과 충돌 판정은 태그 문자열이 아니라 Vehicle 컴포넌트로 합니다.
    // 태그는 오타를 컴파일러가 잡아 주지 못하고, 차가 두 대가 되면 의미가 모호해집니다.

    [Header("충돌 설정")]

    [Tooltip("도보 플레이어와 부딪혔을 때 전달할 충격 세기 배율")]
    public float impactScaleOnPlayer = 1f;

    [Tooltip("도보 플레이어와 부딪혔을 때도 이 적이 사라질지 여부")]
    public bool dieOnFootPlayerHit = false;

    [Header("효과 설정")]
    [Tooltip("피격 시 재생할 파티클 시스템 (이 오브젝트의 자식 또는 컴포넌트)")]
    public ParticleSystem hitEffectParticle;

    [Tooltip("죽었을 때 생성할 파티클 프리팹")]
    public ParticleSystem deathEffectParticle;

    [Tooltip("피격 시 점멸 효과를 줄 렌더러 (자식 오브젝트의 MeshRenderer 등)")] // [추가됨]
    public Renderer visualRenderer; // [추가됨]

    public Light flickerLight; // [추가됨]

    [Tooltip("점멸 지속 시간")] // [추가됨]
    public float flickerDuration = 0.5f; // [추가됨]

    [Tooltip("점멸 간격 (깜빡이는 속도)")] // [추가됨]
    public float flickerInterval = 0.1f; // [추가됨]

    [Header("사운드")]
    [Tooltip("사운드를 재생할 컨트롤러. 비워두면 같은 오브젝트에서 찾습니다.")]
    public EnemySoundController soundController;

    [Tooltip("피격음이 다시 나기까지의 최소 간격(초). " +
             "앙크는 매 프레임 TakeDamage를 호출하므로 이 값이 없으면 소리가 도배됩니다.")]
    public float damageSoundInterval = 0.25f;

    // --- Private Member Variables ---

    /// <summary>
    /// 이 적의 Rigidbody 컴포넌트
    /// </summary>
    private Rigidbody rb;

    /// <summary>
    /// 현재 체력
    /// </summary>
    private float currentHealth;

    /// <summary>
    /// [추가됨] 현재 점멸 효과가 진행 중인지 여부
    /// </summary>
    private bool isFlickering = false; // [추가됨]

    /// <summary>
    /// 피격음을 마지막으로 재생한 시각. 소리 도배를 막는 데 씁니다.
    /// </summary>
    private float lastDamageSoundTime = -99f;

    /// <summary>
    /// 이미 죽음 처리에 들어갔는지 여부. Die가 두 번 실행되는 것을 막습니다.
    /// </summary>
    private bool isDying = false;

    // --- IDamageable ---

    /// <summary>이미 쓰러졌는지 여부입니다.</summary>
    public bool IsDead { get { return currentHealth <= 0f; } }

    // --- Unity Event Functions ---

    /// <summary>
    /// 스크립트가 처음 활성화될 때 호출됩니다.
    /// </summary>
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        currentHealth = maxHealth; // 체력 초기화

        // 사운드 컨트롤러는 있으면 쓰고 없으면 조용히 넘어갑니다.
        if (soundController == null) soundController = GetComponent<EnemySoundController>();

        // Rigidbody 설정 (필요에 따라 조절)
        rb.useGravity = true;
        rb.isKinematic = false;

        // 타겟이 인스펙터에서 설정되지 않았으면 노릴 차량을 찾습니다.
        if (target == null) ResolveTarget();

        if (hitEffectParticle != null)
        {
            hitEffectParticle.Stop(); // 피격 파티클이 자동 재생되지 않도록 초기화
        }

        // [추가됨] visualRenderer 자동 할당 (설정되지 않은 경우)
        if (visualRenderer == null)
        {
            // 자식 오브젝트에서 MeshRenderer를 찾아봅니다.
            visualRenderer = GetComponentInChildren<MeshRenderer>();
            if (visualRenderer == null)
            {
                // MeshRenderer가 없다면 SkinnedMeshRenderer를 찾아봅니다. (애니메이션 모델용)
                visualRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            }

            if (visualRenderer == null)
            {
                // 그래도 없다면 일반 Renderer를 찾아봅니다.
                visualRenderer = GetComponentInChildren<Renderer>();
            }

            if (visualRenderer == null)
            {
                Debug.LogWarning("EnemyController: 점멸 효과를 위한 'visualRenderer'가 할당되지 않았고, 자식 오브젝트에서도 찾을 수 없습니다.");
            }
        }
    }

    /// <summary>
    /// 고정된 시간 간격으로 물리 업데이트 시 호출됩니다.
    /// Rigidbody를 제어할 때 사용합니다.
    /// </summary>
    void FixedUpdate()
    {
        // 타겟이 없으면(아직 차량이 없거나 파괴되었으면) 다시 찾아봅니다.
        // Vehicle.All은 작은 정적 목록이라 매 물리 프레임 확인해도 부담이 없습니다.
        if (target == null) ResolveTarget();

        if (target == null)
        {
            // 그래도 없으면 XZ축 이동을 중지합니다. (Y축 속도는 중력 등을 위해 유지)
            rb.linearVelocity = new Vector3(0, rb.linearVelocity.y, 0);
            return;
        }

        // --- XZ 평면 추적 로직 ---

        // 1. 타겟의 위치와 이 오브젝트의 위치를 가져옵니다.
        Vector3 targetPosition = target.position;
        Vector3 currentPosition = transform.position;

        // 2. Y축(높이)을 0(혹은 동일하게)으로 만들어 XZ 평면상의 위치만 계산합니다.
        Vector3 targetPositionXZ = new Vector3(targetPosition.x, 0, targetPosition.z);
        Vector3 currentPositionXZ = new Vector3(currentPosition.x, 0, currentPosition.z);

        // 3. 타겟을 향하는 방향 벡터를 계산하고 정규화(normalized, 길이 1)합니다.
        Vector3 direction = (targetPositionXZ - currentPositionXZ).normalized;

        // 4. Rigidbody의 속도(velocity)를 설정하여 타겟 방향으로 이동시킵니다.
        // Y축 속도는 현재 Rigidbody의 Y 속도를 유지하여 중력 등이 적용되게 합니다.
        rb.linearVelocity = new Vector3(direction.x * moveSpeed, rb.linearVelocity.y, direction.z * moveSpeed);

        // 5. (선택 사항) 적이 이동하는 방향을 바라보게 합니다.
        if (direction != Vector3.zero) // 방향이 0이 아닐 때만 (제자리일 때 오류 방지)
        {
            // Y축 높이는 현재 오브젝트의 높이를 유지하면서 타겟의 XZ 위치를 바라보게 합니다.
            Vector3 lookPosition = new Vector3(target.position.x, transform.position.y, target.position.z);
            transform.LookAt(lookPosition);
        }
    }

    /// <summary>
    /// 다른 Collider와 물리적 충돌이 시작될 때 호출됩니다.
    /// </summary>
    /// <param name="collision">충돌 관련 정보를 담고 있는 Collision 객체</param>
    void OnCollisionEnter(Collision collision)
    {
        // 1. 도보 플레이어와 부딪혔는지 먼저 확인합니다.
        //    태그가 아니라 컴포넌트로 판정합니다. (PlayerCar가 이미 "Player" 태그를 쓰고 있음)
        PlayerImpactReceiver receiver = collision.collider.GetComponentInParent<PlayerImpactReceiver>();
        if (receiver != null)
        {
            Vector3 direction = receiver.transform.position - transform.position;
            receiver.TakeImpact(impactScaleOnPlayer, direction);

            if (dieOnFootPlayerHit)
            {
                Debug.Log("EnemyController: 도보 플레이어와 충돌하여 파괴됩니다.");
                Die();
            }
            return;
        }

        // 2. 차량과 충돌하면 이 오브젝트를 파괴합니다.
        //    콜라이더가 자식(휠·차체)에 있을 수 있으므로 부모까지 올라가며 찾습니다.
        if (collision.collider != null && collision.collider.GetComponentInParent<Vehicle>() != null)
        {
            Debug.Log("EnemyController: 차량과 충돌하여 파괴됩니다.");
            Die();
        }
    }

    /// <summary>
    /// 노릴 차량을 정합니다. 플레이어가 타고 있는 차가 있으면 그 차를,
    /// 없으면 가장 가까운 차를 고릅니다.
    /// </summary>
    private void ResolveTarget()
    {
        Vehicle prey = Vehicle.GetTargetVehicle(transform.position);
        target = prey != null ? prey.transform : null;
    }

    // --- Public Methods ---

    /// <summary>
    /// [수정됨] 적에게 데미지를 입히는 public 함수입니다.
    /// </summary>
    /// <param name="amount">받은 데미지 양</param>
    public void TakeDamage(float amount)
    {
        // 이미 죽었다면 (체력이 0 이하면) 데미지를 받지 않습니다.
        if (currentHealth <= 0) return;

        // 체력을 깎습니다.
        currentHealth -= amount;

        // 피격 파티클 재생 (이미 재생 중이면 그대로 둡니다)
        if (hitEffectParticle != null && !hitEffectParticle.isPlaying)
        {
            hitEffectParticle.Play();
        }

        // [추가됨] 점멸 효과 시작 (현재 점멸 중이 아닐 때만)
        if (visualRenderer != null && !isFlickering)
        {
            StartCoroutine(FlickerEffect());
        }

        // 피격음. 앙크처럼 지속 피해를 주는 공격은 매 프레임 들어오므로 간격을 둡니다.
        if (soundController != null && Time.time - lastDamageSoundTime >= damageSoundInterval)
        {
            lastDamageSoundTime = Time.time;
            soundController.PlayTakeDamageSound();
        }

        // 체력이 0 이하가 되면
        if (currentHealth <= 0)
        {
            Die(); // 죽음 처리를 합니다.
        }
    }

    // --- Private Methods ---

    /// <summary>
    /// [수정됨] 적이 죽었을 때 처리 (파티클 생성 및 오브젝트 파괴)
    /// </summary>
    private void Die()
    {
        // 충돌과 체력 소진이 같은 프레임에 겹치면 Die가 두 번 불릴 수 있습니다.
        // 사망음이 겹쳐 나지 않도록 한 번만 처리합니다.
        if (isDying) return;
        isDying = true;

        // 사망음은 오브젝트가 파괴된 뒤에도 들려야 하므로 위치 기반으로 재생합니다.
        if (soundController != null) soundController.PlayDeathSound();

        // [추가됨] 죽음 파티클 생성
        if (deathEffectParticle != null)
        {
            // 파티클 이펙트를 현재 위치와 회전값으로 씬에 생성(Instantiate)합니다.
            // 이 파티클 프리팹은 'Play On Awake'가 켜져 있어야 하고,
            // 재생이 끝나면 스스로 파괴되도록 (Main 모듈의 'Stop Action' -> 'Destroy') 설정하는 것이 좋습니다.
            Instantiate(deathEffectParticle, transform.position, transform.rotation);
        }

        Debug.Log(gameObject.name + "가 파괴되었습니다.");

        // 이 GameObject를 씬에서 파괴합니다.
        Destroy(gameObject);
    }

    // --- Coroutines --- [추가됨]

    /// <summary>
    /// [추가됨] 피격 시 렌더러를 깜빡이는 효과
    ///
    /// flickerLight는 인스펙터에서 비워 둘 수 있으므로 반드시 널 검사를 거칩니다.
    /// 또한 점멸이 끝나면 라이트를 <b>원래 상태로</b> 되돌립니다.
    /// (켜져 있던 것을 꺼 버리면 이후로 계속 꺼진 채 남습니다)
    /// </summary>
    private IEnumerator FlickerEffect()
    {
        isFlickering = true; // 점멸 시작
        yield return HitFlicker.Play(visualRenderer, flickerLight, flickerDuration, flickerInterval);
        isFlickering = false; // 점멸 종료
    }
}

