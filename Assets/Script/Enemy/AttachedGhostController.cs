using UnityEngine;
using System.Collections;

/// <summary>
/// [신규] 차량에 부착되어 로컬 좌표로 이동하며 주기적인 피해를 주는 귀신입니다.
/// Rigidbody를 사용하지 않고 부모(차량)의 자식으로 작동합니다.
/// </summary>
public class AttachedGhostController : MonoBehaviour
{
    [Header("귀신 이동 설정")]
    [Tooltip("부모(차량)의 로컬 좌표 기준 목표 지점까지 이동하는 속도")]
    public float moveSpeed = 1.0f;
    [Tooltip("목표 지점과 이 거리만큼 가까워지면 공격을 시작합니다.")]
    public float attackDistance = 1.0f;

    [Header("귀신 공격 설정")]
    [Tooltip("공격 주기 (초)")]
    public float damageInterval = 1.0f;
    [Tooltip("주기마다 입힐 데미지")]
    public int damageToDeal = 5;

    [Header("체력 및 효과 (EnemyController와 유사)")]
    public float maxHealth = 50f;
    public ParticleSystem hitEffectParticle;
    public ParticleSystem deathEffectParticle;
    public Renderer visualRenderer;
    public Light flickerLight;
    public float flickerDuration = 0.5f;
    public float flickerInterval = 0.1f;

    // --- Private Variables ---
    private float currentHealth;
    private bool isFlickering = false;
    private Vector3 targetLocalPosition; // 공격할 목표 지점 (로컬 좌표)
    private TextHealthBar carHealthBar;  // 공격할 대상 (차량의 체력바)
    private float damageTimer;
    private bool hasArrived = false;

    /// <summary>
    /// 스크립트가 처음 활성화될 때 (주로 Start에서) 호출됩니다.
    /// </summary>
    void Start()
    {
        // 렌더러 자동 할당 (EnemyController에서 가져옴)
        if (visualRenderer == null)
        {
            visualRenderer = GetComponentInChildren<MeshRenderer>();
            if (visualRenderer == null) visualRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            if (visualRenderer == null) visualRenderer = GetComponentInChildren<Renderer>();
            if (visualRenderer == null) Debug.LogWarning(gameObject.name + ": 'visualRenderer'가 없어 점멸 효과가 작동하지 않습니다.");
        }

        if (hitEffectParticle != null)
        {
            hitEffectParticle.Stop();
        }
    }

    /// <summary>
    /// GhostSpawner가 호출하여 이 귀신을 초기화합니다.
    /// </summary>
    /// <param name="targetHealthBar">공격할 차량의 체력바</param>
    /// <param name="localTarget">공격할 로컬 좌표</param>
    public void Initialize(TextHealthBar targetHealthBar, Vector3 localTarget)
    {
        this.carHealthBar = targetHealthBar;
        this.targetLocalPosition = localTarget;
        this.currentHealth = maxHealth;
        this.damageTimer = damageInterval;
    }

    /// <summary>
    /// 매 프레임마다 호출됩니다.
    /// </summary>
    void Update()
    {
        if (hasArrived)
        {
            // 1. 목표 지점 도착: 주기적 데미지 처리
            HandleProximityDamage();
        }
        else
        {
            // 2. 이동 중: 목표 지점으로 로컬 좌표 이동
            HandleMovement();
        }
    }

    /// <summary>
    /// 부모(차량) 기준 로컬 좌표로 이동합니다.
    /// </summary>
    private void HandleMovement()
    {
        if (transform.parent == null) return; // 부모가 없으면(오류) 중지

        // 로컬 위치를 목표 로컬 위치로 이동
        transform.localPosition = Vector3.MoveTowards(
            transform.localPosition,
            targetLocalPosition,
            moveSpeed * Time.deltaTime
        );

        // 목표 지점에 도착했는지 확인
        if (Vector3.Distance(transform.localPosition, targetLocalPosition) <= attackDistance)
        {
            hasArrived = true;
            Debug.Log(gameObject.name + "가 차량에 도착했습니다. 공격을 시작합니다.");
        }
    }

    /// <summary>
    /// 목표 지점에 도착했을 때 주기적으로 데미지를 입힙니다.
    /// </summary>
    private void HandleProximityDamage()
    {
        damageTimer -= Time.deltaTime;
        if (damageTimer <= 0f)
        {
            if (carHealthBar != null)
            {
                Debug.Log(gameObject.name + "가 차량에 데미지를 입힙니다!");
                carHealthBar.TakeDamage(damageToDeal);
                // (선택 사항) 데미지를 줄 때마다 효과(파티클 등) 재생
            }
            damageTimer = damageInterval; // 타이머 초기화
        }
    }

    /// <summary>
    /// [Public] PlayerAttacker로부터 데미지를 받습니다.
    /// (EnemyController의 TakeDamage 로직과 동일)
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (currentHealth <= 0) return;

        currentHealth -= amount;

        if (hitEffectParticle != null)
        {
            if (!hitEffectParticle.isPlaying)
            {
                hitEffectParticle.Play();
            }
        }

        if (visualRenderer != null && !isFlickering)
        {
            StartCoroutine(FlickerEffect());
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 귀신이 죽었을 때 처리
    /// (EnemyController의 Die 로직과 동일)
    /// </summary>
    private void Die()
    {
        if (deathEffectParticle != null)
        {
            // 중요: 파티클이 차량을 따라다니지 않도록 부모를 null로 설정하거나,
            // 월드 좌표계에 생성(Instantiate)합니다.
            Instantiate(deathEffectParticle, transform.position, transform.rotation);
        }

        Debug.Log(gameObject.name + "가 파괴되었습니다.");
        Destroy(gameObject); // 이 게임 오브젝트 파괴
    }

    /// <summary>
    /// 피격 시 점멸 효과
    /// (EnemyController의 FlickerEffect 로직과 동일)
    /// </summary>
    private IEnumerator FlickerEffect()
    {
        isFlickering = true;
        float timer = 0f;

        while (timer < flickerDuration)
        {
            if (visualRenderer) visualRenderer.enabled = false;
            if (flickerLight) flickerLight.enabled = false;
            yield return new WaitForSeconds(flickerInterval);
            timer += flickerInterval;

            if (visualRenderer) visualRenderer.enabled = true;
            if (flickerLight) flickerLight.enabled = true;
            yield return new WaitForSeconds(flickerInterval);
            timer += flickerInterval;
        }

        if (visualRenderer) visualRenderer.enabled = true;
        if (flickerLight) flickerLight.enabled = true;
        isFlickering = false;
    }
}
