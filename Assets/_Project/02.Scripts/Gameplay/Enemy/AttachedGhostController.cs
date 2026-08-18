using UnityEngine;
using System.Collections;

/// <summary>
/// [신규] 차량에 부착되어 로컬 좌표로 이동하며 주기적인 피해를 주는 귀신입니다.
/// Rigidbody를 사용하지 않고 부모(차량)의 자식으로 작동합니다.
/// </summary>
public class AttachedGhostController : MonoBehaviour, IDamageable, IHostile
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

    [Tooltip("주기마다 올릴 스트레스 (NeedsSystem이 씬에 없으면 무시됩니다)")]
    public float stressPerAttack = 0.04f;

    [Tooltip("주기마다 올릴 더러움 (귀신이 남기는 흔적)")]
    public float hygieneCostPerAttack = 0.01f;

    [Tooltip("주기마다 차체를 흔드는 세기. 0이면 흔들지 않습니다.")]
    public float carShakeScalePerAttack = 0.35f;

    [Header("체력 및 효과 (EnemyController와 유사)")]
    public float maxHealth = 50f;
    public ParticleSystem hitEffectParticle;
    public ParticleSystem deathEffectParticle;
    public Renderer visualRenderer;
    public Light flickerLight;
    public float flickerDuration = 0.5f;
    public float flickerInterval = 0.1f;

    [Header("사운드")]
    [Tooltip("사운드를 재생할 컨트롤러. 비워두면 같은 오브젝트에서 찾습니다.")]
    public AttachedGhostSoundController soundController;

    [Tooltip("피격음이 다시 나기까지의 최소 간격(초). " +
             "앙크는 매 프레임 TakeDamage를 호출하므로 이 값이 없으면 소리가 도배됩니다.")]
    public float damageSoundInterval = 0.25f;

    // --- Private Member Variables ---

    /// <summary>이 귀신의 남은 체력입니다.</summary>
    private float currentHealth;

    /// <summary>피격 깜빡임 연출이 재생 중인지 여부입니다. 중복 실행을 막습니다.</summary>
    private bool isFlickering = false;

    /// <summary>마지막으로 피격음을 낸 시각입니다. damageSoundInterval 판정에 씁니다.</summary>
    private float lastDamageSoundTime = -99f;

    /// <summary>죽는 연출이 시작되었는지 여부입니다. 죽는 도중 다시 죽지 않도록 막습니다.</summary>
    private bool isDying = false;

    /// <summary>
    /// 쓰러져 물러날 때 알립니다. 스폰한 쪽(GhostSpawner)이 자리를 비우고 풀로 돌려보냅니다.
    /// 연결되어 있지 않으면 예전처럼 스스로 파괴됩니다.
    /// </summary>
    public System.Action<AttachedGhostController> onDespawned;

    /// <summary>점멸 전 라이트의 원래 상태입니다. 풀로 돌아갈 때 이 값으로 되돌립니다.</summary>
    private bool lightDefaultEnabled;

    /// <summary>라이트의 원래 상태를 기억해 두었는지 여부입니다.</summary>
    private bool lightDefaultCached;

    // --- IDamageable ---

    /// <summary>이미 쓰러졌는지 여부입니다.</summary>
    public bool IsDead { get { return currentHealth <= 0f; } }
    private Vector3 targetLocalPosition; // 공격할 목표 지점 (로컬 좌표)
    private VehicleHealth carHealth;     // 공격할 대상 (차량의 내구도)
    private CarImpactShake carImpactShake; // 차체 흔들림 (처음 필요할 때 찾아 캐시합니다)

    /// <summary>다음 주기적 공격까지 남은 시간(초)입니다.</summary>
    private float damageTimer;

    /// <summary>목표 지점에 도달해 차량에 달라붙었는지 여부입니다.</summary>
    private bool hasArrived = false;

    /// <summary>
    /// 스크립트가 처음 활성화될 때 (주로 Start에서) 호출됩니다.
    /// </summary>
    void Start()
    {
        // 사운드 컨트롤러는 있으면 쓰고 없으면 조용히 넘어갑니다.
        if (soundController == null) soundController = GetComponent<AttachedGhostSoundController>();

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

        // 풀에서 재사용될 때 되돌릴 기준입니다.
        // Start는 인스턴스당 한 번만 돌기 때문에 여기서 기억해 두면 계속 유효합니다.
        if (flickerLight != null)
        {
            lightDefaultEnabled = flickerLight.enabled;
            lightDefaultCached = true;
        }
    }

    /// <summary>
    /// 풀로 돌아갈 때 연출 상태를 정리합니다.
    ///
    /// 점멸하는 도중에 죽으면 코루틴이 끊기면서 렌더러가 꺼진 채로 남습니다.
    /// 파괴되던 시절에는 문제가 아니었지만, 이제는 그 상태로 다시 꺼내 쓰게 되므로
    /// <b>보이지 않는 귀신</b>이 나타납니다. 그래서 나갈 때 반드시 되돌립니다.
    /// </summary>
    void OnDisable()
    {
        StopAllCoroutines();
        isFlickering = false;

        if (visualRenderer != null) visualRenderer.enabled = true;
        if (flickerLight != null && lightDefaultCached) flickerLight.enabled = lightDefaultEnabled;
        if (hitEffectParticle != null) hitEffectParticle.Stop();
    }

    /// <summary>
    /// GhostSpawner가 호출하여 이 귀신을 초기화합니다.
    /// </summary>
    /// <param name="targetHealth">공격할 차량의 내구도</param>
    /// <param name="localTarget">공격할 로컬 좌표</param>
    public void Initialize(VehicleHealth targetHealth, Vector3 localTarget)
    {
        this.carHealth = targetHealth;
        this.targetLocalPosition = localTarget;
        this.currentHealth = maxHealth;
        this.damageTimer = damageInterval;

        // 풀에서 다시 꺼내 쓰는 경우 지난번 진행 상태가 그대로 남아 있습니다.
        // 파괴되지 않으므로 필드가 초기화되지 않는다는 점을 반드시 염두에 두어야 합니다.
        hasArrived = false;
        isDying = false;
        lastDamageSoundTime = -99f;

        // 다른 차량에 붙을 수 있으므로 차체 흔들림 참조도 다시 찾게 합니다.
        carImpactShake = null;
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

            // 부착되었으니 속삭임·공격 루프를 시작합니다.
            if (soundController != null) soundController.StartAttackLoop();
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
            if (carHealth != null)
            {
                Debug.Log(gameObject.name + "가 차량에 데미지를 입힙니다!");
                carHealth.TakeDamage(damageToDeal);
            }

            // 한 대 칠 때마다 타격음이 납니다.
            if (soundController != null) soundController.PlayAttackImpact();

            // 귀신에게 시달리면 스트레스와 더러움이 오릅니다.
            NeedsSystem.Report(NeedType.Stress, stressPerAttack);
            NeedsSystem.Report(NeedType.Hygiene, hygieneCostPerAttack);

            // 맞을 때마다 차체가 출렁입니다. (귀신은 차량의 자식으로 붙어 있습니다)
            if (carShakeScalePerAttack > 0f)
            {
                if (carImpactShake == null) carImpactShake = GetComponentInParent<CarImpactShake>();
                if (carImpactShake != null)
                {
                    Vector3 direction = carImpactShake.transform.position - transform.position;
                    carImpactShake.TriggerImpactShake(direction, carShakeScalePerAttack);
                }
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

        // 피격음. 앙크는 매 프레임 들어오므로 간격을 둡니다.
        if (soundController != null && Time.time - lastDamageSoundTime >= damageSoundInterval)
        {
            lastDamageSoundTime = Time.time;
            soundController.PlayTakeDamageSound();
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
        if (isDying) return;
        isDying = true;

        // 루프를 끄고, 오브젝트가 파괴된 뒤에도 들리도록 위치 기반으로 사망음을 재생합니다.
        if (soundController != null) soundController.PlayDeathSound();

        if (deathEffectParticle != null)
        {
            // 중요: 파티클이 차량을 따라다니지 않도록 부모를 null로 설정하거나,
            // 월드 좌표계에 생성(Instantiate)합니다.
            Instantiate(deathEffectParticle, transform.position, transform.rotation);
        }

        Debug.Log(gameObject.name + "가 쓰러졌습니다.");

        // 스폰한 쪽이 풀로 돌려보냅니다. 연결되어 있지 않으면 예전처럼 파괴합니다.
        if (onDespawned != null) onDespawned(this);
        else Destroy(gameObject);
    }

    /// <summary>
    /// 피격 시 점멸 효과
    /// (EnemyController의 FlickerEffect 로직과 동일)
    /// </summary>
    private IEnumerator FlickerEffect()
    {
        isFlickering = true;
        yield return HitFlicker.Play(visualRenderer, flickerLight, flickerDuration, flickerInterval);
        isFlickering = false;
    }
}
