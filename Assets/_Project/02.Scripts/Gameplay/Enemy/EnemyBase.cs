using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 적이 공통으로 갖는 것 — 체력·피격 연출·사망 처리 — 을 한곳에 모읍니다.
    ///
    /// 예전에는 <see cref="EnemyController"/>와 <see cref="AttachedGhostController"/>가
    /// 같은 코드를 각각 복사해서 갖고 있었습니다. 그 사실은 코드에 그대로 적혀 있었습니다.
    /// ("EnemyController의 TakeDamage 로직과 동일", "Die 로직과 동일", "FlickerEffect 로직과 동일")
    /// 복사본이 갈라지면서 실제로 차이도 생겼습니다. 귀신만 풀 재사용을 대비해
    /// 라이트의 원래 상태를 되돌렸고, 추적형 적은 그러지 않았습니다.
    ///
    /// 그래서 <b>둘이 같은 것</b>만 여기로 올립니다. 이동·공격 방식은 서로 완전히 다르므로
    /// 각자 갖습니다. 새 적을 추가할 때는 이 클래스를 상속하고 셋만 구현하면 됩니다.
    ///
    /// <b>컴포넌트를 쪼개지 않고 기반 클래스로 만든 이유가 있습니다.</b>
    /// 연출용 필드를 별도 컴포넌트로 옮기면 Unity가 직렬화 대상을 잃어버려
    /// 적 프리팹 세 개의 인스펙터 연결(렌더러·라이트·파티클)을 손으로 다시 이어야 합니다.
    /// 같은 컴포넌트의 기반 클래스로 올리면 필드 이름이 그대로라 <b>연결이 유지</b>됩니다.
    ///
    /// 체력만은 예외로 <see cref="EnemyHealth"/> 컴포넌트로 분리했습니다.
    /// 그래야 이 프로젝트에서 체력을 다루는 방식이 <see cref="Health"/> 하나로 모입니다.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public abstract class EnemyBase : MonoBehaviour, IDamageable, IHostile
    {
        // --- Public Member Variables ---

        /// <summary>피격 시 재생할 파티클입니다. 이 오브젝트의 자식에 두는 것이 보통입니다.</summary>
        [Header("피격 연출")]
        [Tooltip("피격 시 재생할 파티클 시스템")]
        public ParticleSystem hitEffectParticle;

        /// <summary>쓰러질 때 남길 파티클 프리팹입니다. 풀에서 꺼내 쓰고 스스로 돌아갑니다.</summary>
        [Tooltip("쓰러질 때 남길 파티클 프리팹. 풀에서 꺼내 쓰므로 Stop Action은 신경 쓰지 않아도 됩니다.")]
        public ParticleSystem deathEffectParticle;

        /// <summary>피격 시 깜빡일 렌더러입니다. 비워두면 자식에서 찾습니다.</summary>
        [Tooltip("피격 시 깜빡일 렌더러. 비워두면 자식에서 찾습니다.")]
        public Renderer visualRenderer;

        /// <summary>렌더러와 함께 깜빡일 라이트입니다. 없어도 됩니다.</summary>
        [Tooltip("함께 깜빡일 라이트 (선택)")]
        public Light flickerLight;

        /// <summary>
        /// 내장 점멸을 쓸지 여부입니다.
        ///
        /// Feel 의 MMF_Flicker 를 <see cref="onDamaged"/>에 연결했다면 <b>이 체크를 끄세요.</b>
        /// 둘 다 켜 두면 렌더러를 껐다 켜는 쪽과 재질 색을 바꾸는 쪽이 겹쳐 이상하게 보입니다.
        /// (CarDrive/Feel/레시피 만들기 를 실행하면 자동으로 꺼집니다)
        /// </summary>
        [Tooltip("내장 점멸을 씁니다. Feel 의 MMF_Flicker 를 onDamaged 에 연결했다면 끄세요.")]
        public bool useBuiltInFlicker = true;

        /// <summary>점멸이 이어지는 시간(초)입니다.</summary>
        [Tooltip("점멸 지속 시간(초)")]
        public float flickerDuration = 0.5f;

        /// <summary>점멸 한 번의 간격(초)입니다.</summary>
        [Tooltip("점멸 간격(초). 작을수록 빨리 깜빡입니다.")]
        public float flickerInterval = 0.1f;

        /// <summary>쓰러질 때 떨어뜨릴 재화 덩어리입니다. 비워두면 아무것도 떨어지지 않습니다.</summary>
        [Header("드롭")]
        [Tooltip("쓰러질 때 떨어뜨릴 덩어리. 비워두면 아무것도 떨어지지 않습니다.")]
        public CurrencyPickup dropPrefab;

        /// <summary>떨어뜨릴 덩어리 개수의 하한입니다.</summary>
        [Tooltip("떨어뜨릴 덩어리 개수의 하한")]
        public int dropCountMin = 1;

        /// <summary>떨어뜨릴 덩어리 개수의 상한입니다.</summary>
        [Tooltip("떨어뜨릴 덩어리 개수의 상한")]
        public int dropCountMax = 3;

        /// <summary>덩어리 하나가 주는 양입니다.</summary>
        [Tooltip("덩어리 하나가 주는 양")]
        public int dropValue = 1;

        /// <summary>덩어리가 흩어지는 반경입니다. 한자리에 겹쳐 쌓이지 않게 합니다.</summary>
        [Tooltip("덩어리가 흩어지는 반경(m). 한자리에 겹쳐 쌓이지 않게 합니다.")]
        public float dropScatterRadius = 0.7f;

        /// <summary>덩어리가 떨어질 높이입니다. 바닥에 파묻히지 않게 조금 띄웁니다.</summary>
        [Tooltip("덩어리가 떨어질 높이(m). 바닥에 파묻히지 않게 조금 띄웁니다.")]
        public float dropHeight = 0.5f;

        /// <summary>피해를 입었을 때 호출됩니다.</summary>
        [Header("이벤트")]
        [Tooltip("피해를 입었을 때. Feel 의 MMF_Player 를 여기에 연결하세요.")]
        public UnityEvent onDamaged;

        /// <summary>쓰러졌을 때 호출됩니다. 드롭과 사망 파티클보다 먼저 불립니다.</summary>
        [Tooltip("쓰러졌을 때. 이 게임에서 가장 중요한 연출 지점입니다.")]
        public UnityEvent onDied;

        /// <summary>피격음이 다시 나기까지의 최소 간격(초)입니다.</summary>
        [Header("사운드")]
        [Tooltip("피격음이 다시 나기까지의 최소 간격(초). " +
                 "앙크는 매 프레임 TakeDamage를 호출하므로 이 값이 없으면 소리가 도배됩니다.")]
        public float damageSoundInterval = 0.25f;

        // --- Protected Properties ---

        /// <summary>이 적의 체력입니다. 하위 클래스가 남은 체력을 볼 때 씁니다.</summary>
        protected EnemyHealth Health { get { return health; } }

        /// <summary>이미 쓰러지는 중인지 여부입니다. 사망 연출이 두 번 돌지 않게 하는 데 씁니다.</summary>
        protected bool IsDying { get { return isDying; } }

        // --- Private Member Variables ---

        /// <summary>이 적의 체력 컴포넌트입니다. Awake에서 찾습니다.</summary>
        private EnemyHealth health;

        /// <summary>사망 처리에 들어갔는지 여부입니다. Die가 두 번 실행되는 것을 막습니다.</summary>
        private bool isDying;

        /// <summary>점멸 연출이 재생 중인지 여부입니다. 중복 실행을 막습니다.</summary>
        private bool isFlickering;

        /// <summary>마지막으로 피격음을 낸 시각입니다. 소리 도배를 막는 데 씁니다.</summary>
        private float lastDamageSoundTime = -99f;

        /// <summary>점멸 전 라이트의 원래 상태입니다. 풀로 돌아갈 때 이 값으로 되돌립니다.</summary>
        private bool lightDefaultEnabled;

        /// <summary>라이트의 원래 상태를 기억해 두었는지 여부입니다.</summary>
        private bool lightDefaultCached;

        // --- IDamageable ---

        /// <summary>이미 쓰러졌는지 여부입니다.</summary>
        public bool IsDead { get { return health == null || health.IsDead; } }

        // --- Unity Event Functions ---

        /// <summary>
        /// 체력 컴포넌트와 연출 참조를 찾아 둡니다.
        /// </summary>
        protected virtual void Awake()
        {
            health = GetComponent<EnemyHealth>();
            if (health == null)
            {
                // RequireComponent가 있어 새로 붙이는 프리팹에는 반드시 있지만,
                // 그 이전에 만들어진 프리팹을 대비해 안전망을 둡니다.
                Debug.LogWarning(gameObject.name + ": EnemyHealth가 없어 기본값으로 붙입니다. " +
                                 "프리팹에 EnemyHealth를 추가하고 최대 체력을 설정하세요.", this);
                health = gameObject.AddComponent<EnemyHealth>();
            }

            ResolveVisualRenderer();

            // 풀에서 재사용될 때 되돌릴 기준입니다.
            // Awake는 인스턴스당 한 번만 돌기 때문에 여기서 기억해 두면 계속 유효합니다.
            if (flickerLight != null)
            {
                lightDefaultEnabled = flickerLight.enabled;
                lightDefaultCached = true;
            }
        }

        /// <summary>
        /// 풀에서 다시 꺼내질 때마다 상태를 새것으로 되돌립니다.
        /// 파괴되지 않으므로 지난번 값이 그대로 남아 있다는 점을 반드시 염두에 두어야 합니다.
        /// </summary>
        protected virtual void OnEnable()
        {
            ResetForSpawn();
        }

        /// <summary>
        /// 풀로 돌아갈 때 연출 상태를 정리합니다.
        ///
        /// 점멸하는 도중에 죽으면 코루틴이 끊기면서 렌더러가 꺼진 채로 남습니다.
        /// 파괴되던 시절에는 문제가 아니었지만, 이제는 그 상태로 다시 꺼내 쓰게 되므로
        /// <b>보이지 않는 적</b>이 나타납니다. 그래서 나갈 때 반드시 되돌립니다.
        /// </summary>
        protected virtual void OnDisable()
        {
            StopAllCoroutines();
            RestoreVisuals();
        }

        // --- Public Methods ---

        /// <summary>
        /// 피해를 입힙니다. 앙크처럼 지속 피해를 주는 공격은 매 프레임 호출합니다.
        /// </summary>
        /// <param name="amount">입힐 피해량</param>
        public void TakeDamage(float amount)
        {
            if (health == null || health.IsDead) return;

            health.TakeDamage(amount);

            // 피격 파티클은 이미 재생 중이면 그대로 둡니다.
            if (hitEffectParticle != null && !hitEffectParticle.isPlaying)
            {
                hitEffectParticle.Play();
            }

            if (useBuiltInFlicker && visualRenderer != null && !isFlickering)
            {
                StartCoroutine(FlickerEffect());
            }

            if (Time.time - lastDamageSoundTime >= damageSoundInterval)
            {
                lastDamageSoundTime = Time.time;
                PlayDamageSound();
            }

            if (onDamaged != null) onDamaged.Invoke();

            if (health.IsDead) Die();
        }

        // --- Protected Methods : 하위 클래스가 구현할 것 ---

        /// <summary>피격음을 재생합니다. 적마다 사운드 컨트롤러 타입이 달라 각자 구현합니다.</summary>
        protected abstract void PlayDamageSound();

        /// <summary>사망음을 재생합니다. 오브젝트가 사라진 뒤에도 들려야 합니다.</summary>
        protected abstract void PlayDeathSound();

        /// <summary>
        /// 자리에서 물러납니다. 추적형 적은 스스로 풀로 돌아가고,
        /// 부착형 귀신은 스폰한 쪽에 먼저 알립니다.
        /// </summary>
        protected abstract void Despawn();

        // --- Protected Methods : 하위 클래스가 호출할 것 ---

        /// <summary>
        /// 쓰러뜨립니다. 체력이 0이 되었을 때는 물론, 차량 충돌처럼
        /// 체력과 무관하게 즉시 사라져야 할 때도 이 메서드를 부릅니다.
        /// </summary>
        protected void Die()
        {
            // 충돌과 체력 소진이 같은 프레임에 겹치면 두 번 불릴 수 있습니다.
            if (isDying) return;
            isDying = true;

            // 즉사 경로로 들어오면 체력이 남아 있습니다. 상태를 실제와 맞춰 둡니다.
            // (isDying이 이미 서 있으므로 onDeath가 Die를 다시 불러도 여기서 멈춥니다)
            if (health != null && !health.IsDead) health.TakeDamage(health.CurrentHealth);

            // 연출을 먼저 알립니다. 아래에서 풀로 돌아가므로 그 뒤에 부르면
            // 이미 꺼진 오브젝트에서 이벤트가 도는 셈이 됩니다.
            if (onDied != null) onDied.Invoke();

            PlayDeathSound();

            // 파괴되지 않고 풀로 돌아가므로 파티클도 풀에서 꺼내 씁니다.
            PooledParticleEffect.Spawn(deathEffectParticle, transform.position, transform.rotation);

            SpawnDrops();

            Debug.Log(gameObject.name + "가 쓰러졌습니다.");

            Despawn();
        }

        /// <summary>
        /// 새로 스폰된 것처럼 상태를 되돌립니다. 하위 클래스가 자기 상태를 함께
        /// 되돌리고 싶으면 재정의한 뒤 base를 먼저 부르세요.
        /// </summary>
        protected virtual void ResetForSpawn()
        {
            isDying = false;
            lastDamageSoundTime = -99f;

            if (health != null) health.Revive(health.maxHealth);

            RestoreVisuals();
        }

        // --- Private Methods ---

        /// <summary>
        /// 쓰러진 자리에 재화 덩어리를 흩뿌립니다.
        ///
        /// 자기 자리를 기준으로 삼습니다. 귀신은 차량의 자식이라 로컬 좌표로 움직이지만
        /// 덩어리는 <b>월드에 떨어져 남아야</b> 하므로 부모를 붙이지 않습니다.
        /// 붙이면 차가 떠날 때 덩어리도 따라가 버립니다.
        /// </summary>
        private void SpawnDrops()
        {
            if (dropPrefab == null) return;

            int count = Random.Range(Mathf.Max(0, dropCountMin), Mathf.Max(dropCountMin, dropCountMax) + 1);
            if (count <= 0) return;

            Vector3 origin = transform.position + Vector3.up * dropHeight;

            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * dropScatterRadius;
                Vector3 position = origin + new Vector3(offset.x, 0f, offset.y);

                GameObject instance = PrefabPool.Get(dropPrefab.gameObject, position, Quaternion.identity, null);
                if (instance == null) continue;

                CurrencyPickup pickup = instance.GetComponent<CurrencyPickup>();
                if (pickup != null) pickup.Initialize(pickup.currency, dropValue);
            }
        }

        /// <summary>
        /// 점멸을 한 차례 재생합니다. 본문은 <see cref="HitFlicker"/>가 갖고 있습니다.
        /// </summary>
        private IEnumerator FlickerEffect()
        {
            isFlickering = true;
            yield return HitFlicker.Play(visualRenderer, flickerLight, flickerDuration, flickerInterval);
            isFlickering = false;
        }

        /// <summary>
        /// 연출을 원래 상태로 되돌립니다. 점멸 도중에 끊겨도 보이지 않는 적이 남지 않게 합니다.
        /// </summary>
        private void RestoreVisuals()
        {
            isFlickering = false;

            if (visualRenderer != null) visualRenderer.enabled = true;
            if (flickerLight != null && lightDefaultCached) flickerLight.enabled = lightDefaultEnabled;
            if (hitEffectParticle != null) hitEffectParticle.Stop();
        }

        /// <summary>
        /// 인스펙터에서 비워 둔 렌더러를 자식에서 찾습니다.
        /// 애니메이션 모델이면 SkinnedMeshRenderer일 수 있어 차례로 확인합니다.
        /// </summary>
        private void ResolveVisualRenderer()
        {
            if (visualRenderer != null) return;

            visualRenderer = GetComponentInChildren<MeshRenderer>();
            if (visualRenderer == null) visualRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
            if (visualRenderer == null) visualRenderer = GetComponentInChildren<Renderer>();

            if (visualRenderer == null)
            {
                Debug.LogWarning(gameObject.name + ": visualRenderer를 찾지 못해 점멸 연출이 동작하지 않습니다.", this);
            }
        }
    }
}
