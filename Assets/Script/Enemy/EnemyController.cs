using UnityEngine;
using System.Collections; // [추가됨] 코루틴을 사용하기 위해 필요

/// <summary>
/// 타겟을 XZ 평면(높이 무시)에서 따라가고, 'Player' 태그와 충돌 시 또는 체력이 0이 되면 파괴되는 적 컨트롤러입니다.
/// 이 스크립트는 Rigidbody와 Collider 컴포넌트가 필요합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class EnemyController : MonoBehaviour
{
    // --- Public Member Variables ---

    [Header("추적 설정")]
    [Tooltip("추적할 대상의 Transform (예: 플레이어 차량)")]
    public Transform target;

    [Tooltip("적의 이동 속도")]
    public float moveSpeed = 3f;

    [Tooltip("적의 최대 체력")]
    public float maxHealth = 100f;

    [Header("충돌 설정")]
    [Tooltip("충돌 대상으로 감지할 태그")]
    public string playerTag = "Player";

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

    // --- Private Member Variables ---

    /// <summary>
    /// 이 적의 Rigidbody 컴포넌트
    /// </summary>
    private Rigidbody rb;

    /// <summary>
    /// 추적할 타겟이 유효하게 설정되었는지 여부
    /// </summary>
    private bool isTargetSet = false;

    /// <summary>
    /// 현재 체력
    /// </summary>
    private float currentHealth;

    /// <summary>
    /// [추가됨] 현재 점멸 효과가 진행 중인지 여부
    /// </summary>
    private bool isFlickering = false; // [추가됨]

    // --- Unity Event Functions ---

    /// <summary>
    /// 스크립트가 처음 활성화될 때 호출됩니다.
    /// </summary>
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        currentHealth = maxHealth; // 체력 초기화

        // Rigidbody 설정 (필요에 따라 조절)
        rb.useGravity = true;
        rb.isKinematic = false;

        // 타겟이 인스펙터에서 설정되었는지 확인
        if (target != null)
        {
            isTargetSet = true;
        }
        else
        {
            // 타겟이 없다면, 씬에서 'playerTag'로 찾아봅니다.
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
            {
                target = player.transform;
                isTargetSet = true;
            }
            else
            {
                Debug.LogWarning("EnemyController: 타겟이 설정되지 않았고, '" + playerTag + "' 태그를 가진 오브젝트도 씬에 없습니다.");
                isTargetSet = false;
            }
        }

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
        // 타겟이 없거나 (파괴되었거나) 설정되지 않았으면 추적 로직을 실행하지 않습니다.
        if (!isTargetSet || target == null)
        {
            // 타겟이 없으면 XZ축 이동을 중지합니다. (Y축 속도는 중력 등을 위해 유지)
            rb.velocity = new Vector3(0, rb.velocity.y, 0);
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
        rb.velocity = new Vector3(direction.x * moveSpeed, rb.velocity.y, direction.z * moveSpeed);

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
        // 충돌한 오브젝트의 태그가 'playerTag'와 일치하는지 확인합니다.
        if (collision.gameObject.CompareTag(playerTag))
        {
            // 'Player' 태그와 충돌했다면 이 오브젝트를 파괴합니다.
            Debug.Log("EnemyController: '" + playerTag + "'와 충돌하여 파괴됩니다.");
            Die();
        }
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

        // 피격 파티클 재생
        if (hitEffectParticle != null)
        {
            if (hitEffectParticle.isPlaying)
            {

            }
            else
            {
                hitEffectParticle.Play();
            }
        }
        else
        {
            // 파티클이 할당되지 않았을 경우를 대비한 로그 (선택 사항)
            // Debug.LogWarning("HitEffectParticle이 할당되지 않았습니다.");
        }

        // [추가됨] 점멸 효과 시작 (현재 점멸 중이 아닐 때만)
        if (visualRenderer != null && !isFlickering)
        {
            StartCoroutine(FlickerEffect());
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
    /// </summary>
    private IEnumerator FlickerEffect()
    {
        isFlickering = true; // 점멸 시작
        float timer = 0f;

        while (timer < flickerDuration)
        {
            // 렌더러를 껐다가
            visualRenderer.enabled = false;
            flickerLight.enabled = false; // [추가됨]
            yield return new WaitForSeconds(flickerInterval);
            timer += flickerInterval;

            // 다시 켠다
            visualRenderer.enabled = true;
            flickerLight.enabled = true; // [추가됨]
            yield return new WaitForSeconds(flickerInterval);
            timer += flickerInterval;
        }

        // 코루틴이 끝나면 반드시 렌더러를 다시 켭니다.
        visualRenderer.enabled = true;
        flickerLight.enabled = false; // [추가됨]
        isFlickering = false; // 점멸 종료
    }
}

