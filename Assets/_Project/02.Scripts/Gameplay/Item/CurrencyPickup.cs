using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 땅에 떨어져 있다가 플레이어가 가까이 가면 빨려 들어오는 재화 덩어리입니다.
    /// 귀신을 쓰러뜨리면 <see cref="EnemyBase"/>가 이것을 떨어뜨립니다.
    ///
    /// <b>콜라이더로 판정하지 않습니다.</b> 트리거를 쓰면 물리 레이어와 차량의 복합
    /// 콜라이더에 얽히고, 차를 타고 지나갈 때와 걸어갈 때의 판정이 서로 달라집니다.
    /// 그냥 거리를 재는 편이 싸고 예측 가능합니다. 화면에 동시에 존재하는 덩어리가
    /// 수십 개를 넘을 일이 없기 때문입니다.
    ///
    /// <b>풀에서 재사용됩니다.</b> 파괴되지 않으므로 지난번 값이 그대로 남아 있습니다.
    /// 되돌리는 일은 전부 <see cref="OnEnable"/>에서 합니다.
    ///
    /// 연출은 이벤트로 빼 두었습니다. Feel 을 쓸 때 <see cref="onCollected"/>에
    /// MMF_Player 를 연결하면 코드를 고치지 않고 습득 연출을 붙일 수 있습니다.
    /// </summary>
    public class CurrencyPickup : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>이 덩어리가 주는 재화 종류입니다.</summary>
        [Header("재화")]
        [Tooltip("이 덩어리가 주는 재화 종류")]
        public CurrencyType currency = CurrencyType.Ectoplasm;

        /// <summary>이 덩어리가 주는 양입니다. 떨어뜨리는 쪽이 덮어쓸 수 있습니다.</summary>
        [Tooltip("주는 양. 떨어뜨리는 쪽이 덮어쓸 수 있습니다.")]
        public int amount = 1;

        /// <summary>이 거리 안에 들어오면 플레이어 쪽으로 끌려갑니다.</summary>
        [Header("습득")]
        [Tooltip("이 거리(m) 안에 들어오면 플레이어 쪽으로 끌려갑니다.")]
        public float magnetRadius = 6f;

        /// <summary>이 거리 안으로 들어오면 습득됩니다.</summary>
        [Tooltip("이 거리(m) 안으로 들어오면 습득됩니다.")]
        public float pickupRadius = 1.2f;

        /// <summary>끌려가기 시작할 때의 속도입니다.</summary>
        [Tooltip("끌려가기 시작할 때의 속도(m/s)")]
        public float magnetStartSpeed = 2f;

        /// <summary>끌려가는 동안 초당 붙는 가속입니다. 가까울수록 빨라 보입니다.</summary>
        [Tooltip("끌려가는 동안 초당 붙는 가속(m/s²)")]
        public float magnetAcceleration = 18f;

        /// <summary>떨어진 직후 이만큼(초)은 끌려가지 않습니다. 튀어나가는 연출을 볼 시간입니다.</summary>
        [Tooltip("떨어진 직후 이만큼(초)은 끌려가지 않습니다. 튀어나가는 연출을 볼 시간입니다.")]
        public float magnetDelay = 0.35f;

        /// <summary>이 시간이 지나면 습득되지 않고 사라집니다. 0이면 사라지지 않습니다.</summary>
        [Tooltip("이 시간(초)이 지나면 사라집니다. 0이면 사라지지 않습니다.")]
        public float lifetime = 45f;

        /// <summary>위아래로 흔들리는 폭입니다. 0이면 흔들리지 않습니다.</summary>
        [Header("연출")]
        [Tooltip("위아래로 흔들리는 폭(m). 0이면 흔들리지 않습니다.")]
        public float bobHeight = 0.15f;

        /// <summary>위아래로 흔들리는 속도입니다.</summary>
        [Tooltip("위아래로 흔들리는 속도")]
        public float bobSpeed = 2f;

        /// <summary>제자리에서 도는 속도(초당 도)입니다. 0이면 돌지 않습니다.</summary>
        [Tooltip("제자리에서 도는 속도(초당 도). 0이면 돌지 않습니다.")]
        public float spinSpeed = 90f;

        /// <summary>습득되었을 때 호출됩니다.</summary>
        [Header("이벤트")]
        [Tooltip("습득되었을 때. Feel 의 MMF_Player 를 여기에 연결하세요.")]
        public UnityEvent onCollected;

        /// <summary>끌려가기 시작할 때 한 번 호출됩니다.</summary>
        [Tooltip("끌려가기 시작할 때 한 번")]
        public UnityEvent onMagnetStarted;

        /// <summary>습득되지 못하고 시간이 다 되어 사라질 때 호출됩니다.</summary>
        [Tooltip("시간이 다 되어 사라질 때")]
        public UnityEvent onExpired;

        // --- Private Member Variables ---

        /// <summary>플레이어의 자리입니다. 주행 중이면 차량, 도보면 도보 리그를 따라갑니다.</summary>
        private PlayerModeController player;

        /// <summary>떨어진 뒤 흐른 시간(초)입니다.</summary>
        private float age;

        /// <summary>지금 끌려가고 있는지 여부입니다.</summary>
        private bool isMagnetized;

        /// <summary>이번에 끌려가는 속도입니다. 매 프레임 가속이 붙습니다.</summary>
        private float magnetSpeed;

        /// <summary>이미 습득 처리에 들어갔는지 여부입니다. 두 번 들어가는 것을 막습니다.</summary>
        private bool isCollected;

        /// <summary>흔들림의 기준이 되는 높이입니다. 떨어진 자리에서 잡습니다.</summary>
        private float baseHeight;

        /// <summary>제자리에서 흔들리고 도는 트윈입니다. 끌려가기 시작하면 죽입니다.</summary>
        private Tween idleTween;

        // --- Unity Event Functions ---

        /// <summary>
        /// 풀에서 꺼내질 때마다 상태를 새것으로 되돌립니다.
        /// 파괴되지 않으므로 지난번 값이 그대로 남아 있다는 점을 반드시 염두에 두어야 합니다.
        /// </summary>
        void OnEnable()
        {
            age = 0f;
            isMagnetized = false;
            isCollected = false;
            magnetSpeed = magnetStartSpeed;
            baseHeight = transform.position.y;

            StartIdleTween();
        }

        /// <summary>
        /// 풀로 돌아갈 때 트윈을 반드시 죽입니다.
        ///
        /// <b>파괴되지 않으므로 SetLink 로는 부족합니다.</b> 살아 있는 트윈을 남긴 채
        /// 회수하면 보관함에 있는 동안에도 위치를 계속 움직이고, 다시 꺼냈을 때
        /// 지난번 트윈이 새로 잡은 자리를 덮어씁니다.
        /// </summary>
        void OnDisable()
        {
            KillIdleTween();
        }

        /// <summary>
        /// 거리를 재서 끌려가거나 습득되고, 그동안 제자리에서 흔들립니다.
        /// </summary>
        void Update()
        {
            if (isCollected) return;

            float dt = Time.deltaTime;
            age += dt;

            if (lifetime > 0f && age >= lifetime)
            {
                Expire();
                return;
            }

            Transform target = ResolvePlayer();
            if (target == null) return;

            // <b>거리는 제곱으로 비교합니다.</b> 실제 거리가 필요한 곳이 없기 때문입니다.
            // Vector3.Distance 는 매번 제곱근을 구하는데, 여기서 하는 일은 두 번의
            // 크기 비교뿐이라 양쪽을 제곱해 두면 답이 같습니다.
            // (아래 MoveTowards 는 필요한 거리를 스스로 구합니다)
            float sqrDistance = (target.position - transform.position).sqrMagnitude;

            if (sqrDistance <= pickupRadius * pickupRadius)
            {
                Collect();
                return;
            }

            // 떨어지자마자 빨려 들어가면 튀어나가는 연출이 보이지 않습니다.
            bool canMagnet = age >= magnetDelay && sqrDistance <= magnetRadius * magnetRadius;

            if (!canMagnet)
            {
                if (isMagnetized)
                {
                    // 플레이어가 멀어졌습니다. 제자리 연출을 다시 켭니다.
                    isMagnetized = false;
                    magnetSpeed = magnetStartSpeed;
                    baseHeight = transform.position.y;
                    StartIdleTween();
                }
                return;
            }

            if (!isMagnetized)
            {
                isMagnetized = true;

                // 끌려가는 동안에는 코드가 위치를 직접 씁니다.
                // 제자리 트윈을 살려 두면 같은 값을 두고 다퉈 덜덜 떨립니다.
                KillIdleTween();

                if (onMagnetStarted != null) onMagnetStarted.Invoke();
            }

            magnetSpeed += magnetAcceleration * dt;
            transform.position = Vector3.MoveTowards(transform.position, target.position, magnetSpeed * dt);
        }

        /// <summary>
        /// 씬 뷰에서 습득·자석 범위를 보여 줍니다.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 0.7f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, magnetRadius);

            Gizmos.color = new Color(0.95f, 0.85f, 0.35f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, pickupRadius);
        }

        // --- Public Methods ---

        /// <summary>
        /// 떨어뜨리는 쪽이 종류와 양을 정해 줍니다.
        /// </summary>
        /// <param name="type">줄 재화 종류</param>
        /// <param name="value">줄 양. 1보다 작으면 1로 올립니다.</param>
        public void Initialize(CurrencyType type, int value)
        {
            currency = type;
            amount = Mathf.Max(1, value);

            // 떨어진 자리를 흔들림의 기준으로 다시 잡습니다.
            baseHeight = transform.position.y;
        }

        // --- Private Methods ---

        /// <summary>
        /// 플레이어가 지금 있는 자리를 돌려줍니다.
        /// 주행 중이면 차량이므로, 차를 타고 지나가도 주울 수 있습니다.
        /// </summary>
        /// <returns>따라갈 Transform. 플레이어를 찾지 못하면 null입니다.</returns>
        private Transform ResolvePlayer()
        {
            if (player == null) player = GameContext.Get<PlayerModeController>();
            return player != null ? player.PickupAnchor : null;
        }

        /// <summary>
        /// 제자리에서 위아래로 흔들리고 도는 트윈을 시작합니다.
        ///
        /// 매 프레임 사인파를 계산하던 것을 트윈 하나로 바꿨습니다.
        /// 시작 지연을 덩어리마다 다르게 주어, 여러 개가 한꺼번에 출렁이지 않게 합니다.
        /// </summary>
        private void StartIdleTween()
        {
            KillIdleTween();

            Sequence sequence = DOTween.Sequence().ForWorld(this);

            if (bobHeight > 0f && bobSpeed > 0f)
            {
                float half = 1f / Mathf.Max(0.01f, bobSpeed);

                sequence.Join(transform.DOMoveY(baseHeight + bobHeight, half)
                                       .SetEase(Ease.InOutSine)
                                       .SetLoops(-1, LoopType.Yoyo)
                                       .SetDelay(Random.Range(0f, half)));
            }

            if (spinSpeed != 0f)
            {
                float turn = 360f / Mathf.Abs(spinSpeed);

                sequence.Join(transform.DOLocalRotate(new Vector3(0f, Mathf.Sign(spinSpeed) * 360f, 0f), turn,
                                                      RotateMode.LocalAxisAdd)
                                       .SetEase(Ease.Linear)
                                       .SetLoops(-1, LoopType.Restart));
            }

            idleTween = sequence;
        }

        /// <summary>제자리 트윈을 정리합니다. 두 번 불려도 안전합니다.</summary>
        private void KillIdleTween()
        {
            if (idleTween != null && idleTween.IsActive()) idleTween.Kill();
            idleTween = null;

            // 이 Transform 에 걸린 다른 트윈까지 확실히 정리합니다.
            transform.DOKill();
        }

        /// <summary>
        /// 습득합니다. 지갑이 없어도 덩어리는 사라집니다.
        /// (없는 지갑 때문에 바닥에 영원히 남아 있는 편이 더 나쁩니다)
        /// </summary>
        private void Collect()
        {
            if (isCollected) return;
            isCollected = true;

            KillIdleTween();
            Wallet.Report(currency, amount);

            if (onCollected != null) onCollected.Invoke();

            PrefabPool.Release(gameObject);
        }

        /// <summary>시간이 다 되어 사라집니다.</summary>
        private void Expire()
        {
            if (isCollected) return;
            isCollected = true;

            KillIdleTween();
            if (onExpired != null) onExpired.Invoke();

            PrefabPool.Release(gameObject);
        }
    }
}
