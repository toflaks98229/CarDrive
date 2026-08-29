using UnityEngine;
using UnityEngine.Events;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 마우스 좌클릭으로 들 수 있는 물건에 붙입니다.
    ///
    /// 들고 있는 동안에도 Rigidbody를 살려 두고 속도로 따라오게 하므로,
    /// 벽을 뚫고 지나가지 않고 문틀에 걸리면 자연스럽게 막힙니다.
    /// 실제 이동은 PlayerCarrier가 처리하고, 이 컴포넌트는 상태와 물리 설정 복원을 담당합니다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Carryable : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>조준했을 때 안내 문구에 쓸 이름입니다.</summary>
        [Header("표시")]
        [Tooltip("조준했을 때 안내에 쓸 이름")]
        public string displayName = "";

        /// <summary>인스펙터에서 이름을 비워 두었을 때 쓰던 옛 기본값입니다.</summary>
        private const string LegacyDefaultName = "물건";

        /// <summary>
        /// 체크하면 들어 올릴 때 <see cref="holdEuler"/> 각도로 맞춥니다.
        /// 꺼 두면 집어 든 순간의 각도를 그대로 유지합니다. (기본값)
        /// </summary>
        [Header("들었을 때의 자세")]
        [Tooltip("체크하면 들어 올릴 때 아래 각도로 맞춥니다. " +
                 "꺼 두면 집어 든 순간의 각도를 그대로 유지합니다.")]
        public bool alignToHoldPose = false;

        /// <summary>
        /// 들어 올릴 때 맞출 회전(도)입니다.
        /// <see cref="alignToHoldPose"/>를 켰을 때만 쓰입니다.
        /// </summary>
        [Tooltip("들어 올릴 때 맞출 회전(도). 위 항목을 켰을 때만 쓰입니다.")]
        public Vector3 holdEuler = Vector3.zero;

        /// <summary>들고 있는 동안 회전을 고정할지 여부입니다.</summary>
        [Tooltip("체크하면 들고 있는 동안 회전을 고정합니다.")]
        public bool lockRotationWhileHeld = true;

        /// <summary>들고 있을 때의 이동 감쇠입니다. 클수록 덜 흔들립니다.</summary>
        [Header("물리 (들고 있는 동안)")]
        [Tooltip("들고 있을 때의 이동 감쇠. 클수록 덜 흔들립니다.")]
        public float heldLinearDamping = 12f;

        /// <summary>들고 있을 때의 회전 감쇠입니다.</summary>
        [Tooltip("들고 있을 때의 회전 감쇠")]
        public float heldAngularDamping = 12f;

        /// <summary>물건을 집어 든 순간 한 번 호출됩니다.</summary>
        [Header("이벤트")]
        public UnityEvent onPickedUp;

        /// <summary>물건을 내려놓은 순간 한 번 호출됩니다.</summary>
        public UnityEvent onDropped;

        // --- Public Properties ---

        /// <summary>지금 들려 있는지 여부입니다.</summary>
        /// <summary>
        /// 안내 문구에 쓸 이름입니다.
        ///
        /// 인스펙터에 이름을 적어 두지 않았으면 <b>오브젝트 이름</b>을 씁니다.
        /// 예전에는 기본값이 "물건"이라, 이름을 채우지 않은 것에는 전부
        /// "물건 들기"라고만 떴습니다. 무엇을 줍는지 알 수 없었습니다.
        ///
        /// 복제본의 "(Clone)"과 흔한 접두사는 떼어 냅니다.
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(displayName) && displayName != LegacyDefaultName)
                {
                    return displayName;
                }
                return CleanName(gameObject.name);
            }
        }

        /// <summary>지금 누군가 이 물건을 들고 있는지 여부입니다.</summary>
        public bool IsHeld { get; private set; }

        /// <summary>
        /// 지금 이 물건을 들고 있는 쪽입니다. 아무도 들고 있지 않으면 null 입니다.
        ///
        /// <b>왜 물건이 드는 쪽을 아는가.</b> 들려 있는 동안에도 이 물건은 <b>다른 사정으로</b>
        /// 꺼지거나 사라집니다 — 마시면 감춰지고, 다 꺼낸 봉투는 자기를 없애고,
        /// 세이브를 되돌리면 통째로 치워집니다. 그때 드는 쪽에 알려 주지 않으면
        /// <b>손이 없는 것을 계속 붙잡고</b> 매 물리 프레임마다 그 Rigidbody 를 밀어붙입니다.
        /// 그 Rigidbody 는 이미 물리 세계에서 빠졌거나 사라진 것입니다.
        /// </summary>
        public PlayerCarrier Holder { get; private set; }

        /// <summary>이 물건의 Rigidbody입니다.</summary>
        public Rigidbody Body { get { EnsureBody(); return body; } }

        // --- Private Member Variables ---

        /// <summary>이 물건의 Rigidbody입니다. 들고 있는 동안에도 살려 두고 속도로 따라오게 합니다.</summary>
        private Rigidbody body;

        // 들기 전 물리 설정을 기억해 두었다가 내려놓을 때 되돌립니다.

        /// <summary>들기 전의 중력 사용 여부입니다.</summary>
        private bool cachedUseGravity;

        /// <summary>들기 전의 선형 감쇠 계수입니다.</summary>
        private float cachedLinearDamping;

        /// <summary>들기 전의 각 감쇠 계수입니다.</summary>
        private float cachedAngularDamping;

        /// <summary>들기 전의 보간 방식입니다.</summary>
        private RigidbodyInterpolation cachedInterpolation;

        /// <summary>들기 전의 충돌 감지 방식입니다.</summary>
        private CollisionDetectionMode cachedCollisionMode;

        /// <summary>
        /// 물리 설정을 이미 기억해 두었는지 여부입니다.
        /// 들었다 놓기를 반복해도 원본이 덮이지 않도록 한 번만 기억합니다.
        /// </summary>
        private bool cached;

        // --- Unity Event Functions ---

        /// <summary>
        /// 들고 다닐 때 조작할 Rigidbody 참조를 가져옵니다.
        /// </summary>
        void Awake()
        {
            EnsureBody();
        }

        /// <summary>
        /// 들려 있는 채로 꺼지면 손에서 빠집니다.
        ///
        /// 마시는 절차는 병을 <c>SetActive(false)</c> 로 감춥니다. 그 순간 이 Rigidbody 는
        /// 물리 세계에서 빠지는데, 드는 쪽은 그것을 모른 채 매 물리 프레임마다 속도와
        /// 회전을 밀어 넣습니다. 감춰졌다 다시 켜지는 빈 병은 그대로 손에 붙은 채 되살아나고,
        /// 꺼졌다 켜지면서 <b>플레이어와의 충돌 무시까지 풀려</b> 제 몸을 밀어냅니다.
        /// </summary>
        void OnDisable()
        {
            ReleaseFromHolder();
        }

        /// <summary>들려 있는 채로 사라지면 손에서 빠집니다. (다 꺼낸 봉투가 자기를 없앨 때)</summary>
        void OnDestroy()
        {
            ReleaseFromHolder();
        }

        // --- Public Methods ---

        /// <summary>
        /// 들리기 시작할 때 PlayerCarrier가 호출합니다.
        /// </summary>
        public void OnPickedUp()
        {
            OnPickedUp(null);
        }

        /// <summary>
        /// 들리기 시작할 때 PlayerCarrier가 호출합니다.
        /// </summary>
        /// <param name="holder">이 물건을 드는 쪽. 꺼지거나 사라질 때 여기에 알립니다.</param>
        public void OnPickedUp(PlayerCarrier holder)
        {
            if (IsHeld) return;
            IsHeld = true;
            Holder = holder;

            EnsureBody();

            if (!cached)
            {
                cachedUseGravity = body.useGravity;
                cachedLinearDamping = body.linearDamping;
                cachedAngularDamping = body.angularDamping;
                cachedInterpolation = body.interpolation;
                cachedCollisionMode = body.collisionDetectionMode;
                cached = true;
            }

            body.useGravity = false;
            body.linearDamping = heldLinearDamping;
            body.angularDamping = heldAngularDamping;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            // 빠르게 움직이는 동안 벽을 통과하지 않도록 합니다.
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            if (onPickedUp != null) onPickedUp.Invoke();
        }

        /// <summary>
        /// 내려놓을 때 PlayerCarrier가 호출합니다.
        /// </summary>
        public void OnDropped()
        {
            if (!IsHeld) return;
            IsHeld = false;
            Holder = null;

            EnsureBody();

            // 사라지는 중에 놓이는 경우가 있습니다. (다 먹은 물건, 치워지는 세이브)
            // 되돌릴 몸이 이미 없으면 되돌릴 것도 없습니다.
            if (cached && body != null)
            {
                body.useGravity = cachedUseGravity;
                body.linearDamping = cachedLinearDamping;
                body.angularDamping = cachedAngularDamping;
                body.interpolation = cachedInterpolation;
                body.collisionDetectionMode = cachedCollisionMode;
            }

            if (onDropped != null) onDropped.Invoke();
        }

        /// <summary>
        /// 들고 있는 쪽의 손에서 빠져나옵니다.
        ///
        /// <b>물건이 사라지거나 꺼지기 <em>전에</em> 부르세요.</b> 그래야 물리 설정이
        /// 아직 살아 있는 Rigidbody 위에서 되돌려집니다. 마시기·꺼내기처럼
        /// 상호작용이 들고 있던 물건을 가져가는 자리가 여기입니다.
        /// </summary>
        public void ReleaseFromHolder()
        {
            if (!IsHeld) return;

            PlayerCarrier holder = Holder;

            // 드는 쪽이 먼저 사라졌을 수도 있습니다. 그때는 스스로 되돌립니다.
            if (holder != null) holder.ReleaseHeld(this);
            else OnDropped();
        }

        // --- Private Methods ---

        /// <summary>
        /// Rigidbody 참조가 없으면 찾습니다.
        ///
        /// <b><c>Awake</c> 에만 맡기지 않는 이유가 있습니다.</b> 에디터 테스트에서는
        /// <c>Awake</c> 가 아예 돌지 않아, 그대로 두면 첫 <see cref="OnPickedUp"/> 에서
        /// 널 참조가 납니다. (<c>NeedsSystem.EnsureInitialized</c> 와 같은 사정입니다)
        /// 두 번 불려도 안전합니다.
        /// </summary>
        private void EnsureBody()
        {
            if (body == null) body = GetComponent<Rigidbody>();
        }

        /// <summary>
        /// 오브젝트 이름을 사람이 읽기 좋게 다듬습니다.
        /// </summary>
        /// <param name="raw">GameObject 이름</param>
        /// <returns>"(Clone)"과 밑줄을 정리한 이름</returns>
        private static string CleanName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return LegacyDefaultName;

            int clone = raw.IndexOf("(Clone)", System.StringComparison.Ordinal);
            if (clone >= 0) raw = raw.Substring(0, clone);

            return raw.Replace('_', ' ').Trim();
        }

    }
}
