using System.Collections.Generic;
using UnityEngine;
using CarDrive.Common;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// 마우스 좌클릭으로 물건을 들고 내려놓습니다.
    ///
    /// 조준점에 Carryable이 걸려 있을 때만 집을 수 있고, 들고 있는 동안에는
    /// 물체가 속도로 손 위치를 따라오므로 벽이나 문틀에 걸리면 그대로 막힙니다.
    /// 이 컴포넌트는 PlayerInteractor와 같은 카메라 오브젝트에 붙입니다.
    /// </summary>
    public class PlayerCarrier : MonoBehaviour
    {
        // --- Public Member Variables ---

        /// <summary>물건이 따라올 손 위치입니다. 비워두면 카메라 앞에 자동으로 만듭니다.</summary>
        [Header("연동")]
        [Tooltip("물건이 따라올 손 위치. 비워두면 카메라 앞에 자동으로 만듭니다.")]
        public Transform holdPoint;

        /// <summary>
        /// 들고 있는 물건과 충돌을 무시할 플레이어 콜라이더입니다. 비워두면 자동으로 찾습니다.
        /// 이것이 없으면 들어 올린 물건이 자기 몸에 부딪혀 플레이어를 밀어냅니다.
        /// </summary>
        [Tooltip("들고 있는 물건과 충돌을 무시할 플레이어 콜라이더. 비워두면 자동으로 찾습니다.")]
        public Collider playerCollider;

        // 들기 / 내려놓기 버튼은 GameInput이 소유합니다. (GameAction.Carry)
        // 앙크 공격과 같은 좌클릭을 나눠 쓰므로, 두 곳이 같은 바인딩을 보게 하는 편이 안전합니다.

        /// <summary>
        /// 조준 광선을 쏠 기준입니다.
        /// 비워두면 <see cref="PlayerAim.Resolve"/>가 이 오브젝트의 카메라나 메인 카메라를 찾아 줍니다.
        /// </summary>
        [Header("조준")]
        [Tooltip("조준 광선을 쏠 기준. 비워두면 이 오브젝트가 카메라인지 확인하고, 아니면 Camera.main을 씁니다.")]
        public Transform aimSource;

        /// <summary>이 거리(m) 안에서 화면 중앙에 걸린 물건만 집을 수 있습니다.</summary>
        [Tooltip("이 거리 안에서 화면 중앙에 걸린 물건만 집을 수 있습니다.")]
        public float pickupDistance = 2.5f;

        /// <summary>
        /// 집을 물건을 찾을 레이어입니다.
        /// 물건이 물리로 굴러다녀야 하므로 상호작용 레이어와 따로 둡니다.
        /// </summary>
        [Tooltip("집을 물건을 찾을 레이어. 물건이 물리로 굴러다녀야 하므로 상호작용 레이어와 따로 둡니다.")]
        public LayerMask carryableLayers = ~0;

        /// <summary><see cref="holdPoint"/>를 자동 생성할 때 카메라로부터 떨어뜨릴 거리(m)입니다.</summary>
        [Header("손 위치")]
        [Tooltip("holdPoint를 자동 생성할 때 카메라로부터의 거리")]
        public float holdDistance = 1.1f;

        /// <summary><see cref="holdPoint"/>를 자동 생성할 때 적용할 높이 오프셋(m)입니다.</summary>
        [Tooltip("holdPoint를 자동 생성할 때의 높이 오프셋")]
        public float holdHeightOffset = -0.15f;

        /// <summary>물건이 손 위치로 끌려오는 속도입니다. 클수록 손에 딱딱 붙습니다.</summary>
        [Header("따라오는 방식")]
        [Tooltip("손 위치로 끌려오는 속도. 클수록 딱딱 붙습니다.")]
        public float followSpeed = 12f;

        /// <summary>따라오는 속도의 상한(m/s)입니다. 너무 크면 물건이 벽을 뚫을 수 있습니다.</summary>
        [Tooltip("따라오는 최대 속도. 너무 크면 벽을 뚫을 수 있습니다.")]
        public float maxFollowSpeed = 8f;

        /// <summary>집어 든 순간의 각도를 유지하도록 회전을 맞추는 속도입니다.</summary>
        [Tooltip("회전을 맞추는 속도")]
        public float rotationSpeed = 12f;

        /// <summary>
        /// 손에서 이 거리(m)보다 멀어지면 자동으로 놓습니다.
        /// 물건이 벽이나 문틀에 끼어 따라오지 못할 때를 대비한 안전장치입니다.
        /// </summary>
        [Header("놓치는 조건")]
        [Tooltip("손에서 이 거리보다 멀어지면 자동으로 놓습니다. (벽에 끼었을 때 대비)")]
        public float breakDistance = 2.2f;

        /// <summary>이 질량보다 무거우면 들 수 없습니다.</summary>
        [Tooltip("이 질량보다 무거우면 들 수 없습니다.")]
        public float maxCarryMass = 40f;

        /// <summary>내려놓을 때 앞으로 밀어내는 힘입니다. 0이면 그 자리에 놓습니다.</summary>
        [Header("던지기")]
        [Tooltip("내려놓을 때 앞으로 밀어내는 힘. 0이면 그 자리에 놓습니다.")]
        public float throwImpulse = 2.5f;

        /// <summary>
        /// 집을 수 있는 물건을 조준했을 때의 안내 문구 서식입니다. <c>{0}</c>에 물건 이름이 들어갑니다.
        /// 기본값은 이름만 보여 줍니다. 키 안내를 붙이려면 "좌클릭: {0} 들기"처럼 씁니다.
        /// </summary>
        [Header("문구 (다국어 대응)")]
        [Tooltip("집을 수 있는 물건을 조준했을 때의 문구. {0}에는 물건 이름이 들어갑니다. " +
                 "지금은 이름만 보여 줍니다. 키 안내를 붙이려면 \"좌클릭: {0} 들기\" 처럼 쓰세요.")]
        public string pickupFormat = "{0}";

        /// <summary>들고 있을 때 표시할 안내 문구입니다.</summary>
        [Tooltip("들고 있을 때 표시할 문구")]
        public string dropLabel = "내려놓기";

        // --- Public Properties ---

        /// <summary>지금 들고 있는 물건입니다. (없으면 null)</summary>
        public Carryable Held { get; private set; }

        /// <summary>무언가 들고 있는지 여부입니다.</summary>
        public bool IsCarrying { get { return Held != null; } }

        /// <summary>조준점에 걸린 집을 수 있는 물건입니다. (없으면 null)</summary>
        public Carryable Target { get; private set; }

        /// <summary>
        /// 좌클릭이 들기/내려놓기에 쓰이는 상황인지 여부입니다.
        /// <see cref="PlayerAttacker"/>가 이 값을 보고 앙크를 꺼낼지 판단합니다.
        /// </summary>
        public bool UsesLeftClick { get { return IsCarrying || Target != null; } }

        // --- Private Member Variables ---

        /// <summary>
        /// 직전 프레임에 조준점이 맞힌 콜라이더입니다.
        ///
        /// <b>왜 들고 있는가.</b> <c>GetComponentInParent&lt;Carryable&gt;</c> 는 콜라이더에서 부모 체인을 끝까지
        /// 거슬러 올라가며 타입을 확인합니다. 그런데 조준점은 대개 여러 프레임 동안
        /// <b>같은 것을 보고 있습니다</b> — 문을 바라보는 1초 동안 같은 탐색을 60번 합니다.
        /// 맞힌 콜라이더가 직전과 같으면 결과도 같으므로 그 탐색을 건너뜁니다.
        ///
        /// 콜라이더가 파괴되면 레이캐스트가 더 이상 맞히지 못하므로 자연히 무효가 됩니다.
        /// (한 콜라이더의 대상 컴포넌트를 실행 중에 갈아 끼우는 경우는 가정하지 않습니다)
        /// </summary>
        private Collider lastHitCollider;

        /// <summary>
        /// 들고 있는 동안 플레이어와의 충돌을 꺼 둔 콜라이더들입니다.
        /// 내려놓을 때 이 목록을 보고 충돌을 되살립니다.
        /// </summary>
        private readonly List<Collider> ignoredColliders = new List<Collider>();

        /// <summary>
        /// 직전에 찾았을 때 <see cref="Target"/> 이 실제로 있었는지 여부입니다.
        /// 있었는데 지금 없다면 그 사이에 사라진 것이므로 캐시를 버려야 합니다.
        /// </summary>
        private bool targetFound;

        /// <summary>조준·손 위치·던지는 방향의 기준입니다. 보통 메인 카메라입니다.</summary>
        private Transform aimTransform;

        /// <summary>
        /// 손 기준으로 본 물건의 회전입니다. 집어 든 순간의 각도를 기억해 두었다가
        /// 들고 있는 동안 그대로 유지합니다. 그래서 <b>들어 올릴 때 물건이 홱 돌아가지 않습니다.</b>
        /// </summary>
        private Quaternion heldRotationOffset = Quaternion.identity;

        // --- Unity Event Functions ---

        /// <summary>
        /// 자신을 레지스트리에 등록합니다. 다른 컴포넌트가 Start에서 찾아 씁니다.
        /// (등록은 Awake, 조회는 Start — Unity가 모든 Awake를 끝낸 뒤 Start를 부릅니다)
        /// </summary>
        void Awake()
        {
            GameContext.Register(this);
        }

        /// <summary>등록을 해제합니다.</summary>
        void OnDestroy()
        {
            GameContext.Unregister(this);
        }

        /// <summary>
        /// 물건을 들 손 위치와 플레이어 콜라이더 참조를 준비합니다.
        /// </summary>
        void Start()
        {
            // "카메라에 붙어 있다"는 가정을 주석이 아니라 코드로 확인합니다.
            // 손 위치를 이 기준의 자식으로 만들기 때문에 반드시 먼저 정해야 합니다.
            aimTransform = PlayerAim.Resolve(aimSource, this);

            if (holdPoint == null) CreateHoldPoint();
            // 씬의 아무 CharacterController가 아니라 '플레이어의 것'이어야 합니다.
            // 이 컴포넌트는 카메라에 붙어 있고 카메라는 탑승할 때 차량 밑으로 옮겨지므로,
            // 부모를 거슬러 찾는 방법은 쓸 수 없습니다. 도보 리그를 아는 쪽에 물어봅니다.
            if (playerCollider == null)
            {
                PlayerModeController mode = GameContext.Resolve<PlayerModeController>(this);
                if (mode != null && mode.footRig != null)
                {
                    playerCollider = mode.footRig.GetComponent<CharacterController>();
                }
            }
            if (playerCollider == null)
            {
                GameLog.Warn(GameLog.Channel.Player, "PlayerCarrier: 플레이어 콜라이더를 찾지 못해 들고 있는 물건과 충돌합니다.", this);
            }
        }

        /// <summary>
        /// 조준 대상을 갱신하고 좌클릭으로 들기·내려놓기를 처리합니다.
        /// 벽에 끼어 손에서 너무 멀어지면 놓칩니다.
        /// </summary>
        void Update()
        {
            // 손에 든 것이 아직 붙잡을 수 있는 상태인지 <b>입력보다 먼저</b> 확인합니다.
            // 이것은 조작이 아니라 지켜야 할 규칙이라, 오버레이가 떠 있어도 멈추지 않습니다.
            DropIfUnholdable();

            // 오버레이 버튼을 누르는 클릭이 들기/내려놓기로 들어가지 않게 합니다.
            //
            // <b>여기는 Suspended를 직접 봅니다.</b> 클릭만 막으면 되는 것이 아니라,
            // 조준 대상 갱신(UpdateTarget)과 놓침 판정도 함께 멈춰야 합니다.
            // 들고 있던 것은 그대로 유지됩니다.
            if (GameInput.Suspended) return;

            UpdateTarget();

            if (GameInput.CarryPressed)
            {
                if (IsCarrying) Drop(true);
                else if (Target != null) PickUp(Target);
            }

            // 벽에 끼어 손에서 너무 멀어지면 놓칩니다.
            if (IsCarrying && holdPoint != null)
            {
                // 놓칠 만큼 멀어졌는지만 보면 되므로 제곱끼리 비교합니다.
                float sqrDistance = (Held.Body.position - holdPoint.position).sqrMagnitude;
                if (sqrDistance > breakDistance * breakDistance) Drop(false);
            }
        }

        /// <summary>
        /// 들고 있는 물건을 손 위치로 끌어당깁니다.
        /// 위치를 직접 대입하지 않고 속도로 움직이므로 벽을 뚫지 않고 문틀에 걸립니다.
        /// </summary>
        void FixedUpdate()
        {
            if (holdPoint == null) return;

            // 꺼졌거나 사라진 것을 미는 일이 없도록 여기서 한 번 더 봅니다.
            // Update 와 FixedUpdate 사이에도 물건은 사라집니다.
            if (DropIfUnholdable() || !IsCarrying) return;

            Rigidbody body = Held.Body;

            // 위치: 손 위치로 향하는 속도를 직접 넣습니다. (질량과 무관하게 같은 느낌)
            Vector3 delta = holdPoint.position - body.position;
            Vector3 velocity = delta * followSpeed;
            if (velocity.magnitude > maxFollowSpeed) velocity = velocity.normalized * maxFollowSpeed;

            // 유한하지 않은 값은 <b>물리 엔진에 넣지 않습니다.</b>
            //
            // 손과 몸 사이에 물건이 끼면 서로 밀어내는 힘이 물려 값이 발산하고,
            // 한 번 NaN 이 들어간 Rigidbody 는 그 프레임의 물리 계산 전체를 무너뜨립니다.
            // 그렇게 될 바에는 놓습니다.
            if (!IsFinite(velocity))
            {
                GameLog.Warn(GameLog.Channel.Player,
                    "PlayerCarrier: 따라오는 속도가 계산되지 않아 " + Held.displayName + "을(를) 놓습니다.", this);
                Drop(false);
                return;
            }

            body.linearVelocity = velocity;

            // 회전: 집어 들 때의 자세를 손을 따라 그대로 유지합니다.
            if (Held.lockRotationWhileHeld)
            {
                Quaternion target = holdPoint.rotation * heldRotationOffset;
                body.MoveRotation(Quaternion.Slerp(body.rotation, target, rotationSpeed * Time.fixedDeltaTime));
                body.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// 들고 있던 물건을 놓아 물리 설정이 복원되지 않은 채 남지 않게 합니다.
        /// </summary>
        void OnDisable()
        {
            // 하차나 모드 전환으로 꺼질 때 들고 있던 것을 놓습니다.
            if (IsCarrying) Drop(false);
        }

        // --- Public Methods ---

        /// <summary>
        /// 화면에 표시할 안내 문구를 만들어 돌려줍니다.
        /// </summary>
        /// <returns>
        /// 들고 있으면 <see cref="dropLabel"/>, 집을 수 있는 물건을 조준 중이면
        /// <see cref="pickupFormat"/>에 이름을 채운 문구. 둘 다 아니면 빈 문자열입니다.
        /// </returns>
        public string GetPrompt()
        {
            if (IsCarrying) return dropLabel;
            if (Target != null) return string.Format(pickupFormat, Target.DisplayName);
            return "";
        }

        /// <summary>
        /// 물건을 집어 듭니다.
        /// 이미 누가 들고 있거나, 리지드바디가 없거나, <see cref="maxCarryMass"/>보다 무거우면 아무 일도 하지 않습니다.
        /// </summary>
        /// <param name="target">집어 들 대상</param>
        public void PickUp(Carryable target)
        {
            if (target == null || target.IsHeld) return;
            if (target.Body == null) return;

            // 꺼져 있는 것은 집지 않습니다. 물리 세계에 없는 몸을 손이 밀게 됩니다.
            if (!target.isActiveAndEnabled) return;

            if (target.Body.mass > maxCarryMass)
            {
                GameLog.Info(GameLog.Channel.Player, "PlayerCarrier: " + target.displayName + "은(는) 너무 무겁습니다.");
                return;
            }

            // 손은 하나입니다. 들고 있던 것을 남겨 두면 그것이 중력이 꺼진 채로 허공에 굳습니다.
            // (들지 못할 것을 확인한 뒤에 놓습니다. 못 드는 것을 겨눴다고 손의 것을 잃으면 곤란합니다)
            if (IsCarrying) Drop(false);

            Held = target;

            // 집어 든 순간의 각도를 손 기준으로 기억해 둡니다.
            // 이렇게 해야 물건이 들리면서 제멋대로 정렬되지 않고, 놓여 있던 자세 그대로 딸려옵니다.
            heldRotationOffset = ResolveHeldRotation(target);

            target.OnPickedUp(this);
            IgnorePlayerCollision(target, true);

            GameLog.Info(GameLog.Channel.Player, "PlayerCarrier: " + target.displayName + "을(를) 들었습니다.");
        }

        /// <summary>
        /// 들고 있던 물건을 내려놓습니다.
        /// </summary>
        /// <param name="throwForward">앞으로 밀어낼지 여부. 자동으로 놓칠 때는 false입니다.</param>
        public void Drop(bool throwForward)
        {
            if (!IsCarrying) return;

            Carryable dropped = Held;
            Held = null;

            // <b>물리 설정을 먼저 되돌립니다.</b> 충돌 정리는 콜라이더가 꺼져 있으면 건너뛰는데,
            // 그 뒤에 두었다가 순서가 엇갈리면 중력이 꺼진 채로 남아 물건이 허공에 굳습니다.
            dropped.OnDropped();
            IgnorePlayerCollision(dropped, false);

            if (throwForward && throwImpulse > 0f && dropped.Body != null)
            {
                Transform aim = aimTransform != null ? aimTransform : transform;
                dropped.Body.AddForce(aim.forward * throwImpulse, ForceMode.VelocityChange);
            }

            GameLog.Info(GameLog.Channel.Player, "PlayerCarrier: " + dropped.displayName + "을(를) 내려놓았습니다.");
        }

        /// <summary>
        /// 지정한 물건이 지금 들고 있는 것이면 손에서 놓습니다.
        ///
        /// <b>물건 쪽에서 부르는 길입니다.</b> 마시려고 감춰지거나, 다 꺼낸 봉투가 자기를
        /// 없애거나, 세이브를 되돌리며 치워질 때 <see cref="Carryable"/> 이 이것을 부릅니다.
        /// 던지지 않고 그 자리에 놓습니다.
        /// </summary>
        /// <param name="target">손에서 뺄 물건. 들고 있는 것과 다르면 아무 일도 하지 않습니다.</param>
        public void ReleaseHeld(Carryable target)
        {
            if (target == null || !ReferenceEquals(target, Held)) return;

            Drop(false);
        }

        // --- Private Methods ---

        /// <summary>
        /// 들고 있는 것이 더 이상 붙잡을 수 없는 상태면 놓습니다.
        ///
        /// <b>무엇이 붙잡을 수 없는 상태인가.</b> 사라졌거나, 꺼졌거나, Rigidbody 를 잃은 것입니다.
        /// 셋 다 <b>물리 세계에 없는</b> 몸이라, 계속 붙잡고 속도를 밀어 넣어 봐야
        /// 아무 데도 닿지 않고, 다시 켜지는 순간 손에 붙은 채로 되살아납니다.
        ///
        /// 보통은 <see cref="Carryable"/> 이 꺼지거나 사라지면서 스스로 알려 주므로 여기까지 오지 않습니다.
        /// 이것은 그 통보가 닿지 못하는 경우(파괴 순서, 씬 정리)를 위한 마지막 그물입니다.
        /// </summary>
        /// <returns>여기서 놓았으면 true</returns>
        private bool DropIfUnholdable()
        {
            // 유니티의 == 는 파괴된 것을 null 로 봅니다. 그래서 '사라졌다'는 여기서 걸립니다.
            if (Held == null)
            {
                if (!ReferenceEquals(Held, null)) ForgetHeld();
                return false;
            }

            Rigidbody body = Held.Body;
            if (body != null && Held.isActiveAndEnabled && body.gameObject.activeInHierarchy) return false;

            Drop(false);
            return true;
        }

        /// <summary>
        /// 이미 사라진 물건의 흔적을 지웁니다. 되돌릴 물리 설정도 함께 사라졌습니다.
        /// </summary>
        private void ForgetHeld()
        {
            Held = null;
            ignoredColliders.Clear();
        }

        /// <summary>세 성분이 모두 유한한 값인지 확인합니다.</summary>
        /// <param name="value">확인할 벡터</param>
        /// <returns>NaN 도 무한대도 아니면 true</returns>
        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        /// <summary>
        /// 들고 있는 동안 유지할 회전을 손 기준으로 계산합니다.
        /// </summary>
        /// <param name="target">집어 든 물건</param>
        /// <returns>
        /// 기본적으로 집어 든 순간의 각도(손 기준)입니다.
        /// alignToHoldPose를 켠 물건만 지정된 자세로 맞춥니다.
        /// </returns>
        private Quaternion ResolveHeldRotation(Carryable target)
        {
            if (target.alignToHoldPose) return Quaternion.Euler(target.holdEuler);
            if (holdPoint == null) return Quaternion.identity;

            // 손 기준으로 본 현재 각도. 손이 움직여도 이 관계가 유지됩니다.
            return Quaternion.Inverse(holdPoint.rotation) * target.Body.rotation;
        }

        /// <summary>
        /// 조준점에 걸린 물건을 갱신합니다. 들고 있는 동안에는 새 대상을 찾지 않습니다.
        /// </summary>
        private void UpdateTarget()
        {
            // 들고 있는 동안에는 새 대상을 찾지 않습니다.
            // 캐시도 함께 비웁니다. 그러지 않으면 내려놓은 뒤 같은 것을 계속 보고 있을 때
            // "직전과 같다"는 이유로 건너뛰어, 대상이 영영 잡히지 않습니다.
            if (IsCarrying)
            {
                lastHitCollider = null;
                Target = null;
                targetFound = false;
                return;
            }

            // 원근 카메라에서 카메라 정면 = 화면 중앙이므로 이 레이가 곧 조준점입니다.
            RaycastHit hit;
            bool didHit = Physics.Raycast(
                aimTransform.position,
                aimTransform.forward,
                out hit,
                pickupDistance,
                carryableLayers,
                QueryTriggerInteraction.Ignore
            );

            if (!didHit)
            {
                lastHitCollider = null;
                Target = null;
                targetFound = false;
                return;
            }

            // 직전 프레임과 같은 것을 보고 있으면 이미 찾아 둔 결과가 그대로 유효합니다.
            // 찾아 둔 것이 그 사이에 사라졌다면(빈 병처럼 컴포넌트만 없어지는 경우)
            // 캐시를 믿지 않고 다시 찾습니다.
            if (hit.collider == lastHitCollider && !(targetFound && Target == null)) return;

            lastHitCollider = hit.collider;
            Target = hit.collider.GetComponentInParent<Carryable>();
            targetFound = Target != null;
        }

        /// <summary>
        /// 카메라 앞에 손 위치를 만듭니다.
        /// 카메라의 자식이므로 탑승·하차로 카메라가 옮겨가도 함께 따라갑니다.
        /// </summary>
        private void CreateHoldPoint()
        {
            GameObject go = new GameObject("HoldPoint");
            go.transform.SetParent(aimTransform, false);
            go.transform.localPosition = new Vector3(0f, holdHeightOffset, holdDistance);
            go.transform.localRotation = Quaternion.identity;
            holdPoint = go.transform;
        }

        /// <summary>
        /// 들고 있는 동안 플레이어 콜라이더와 부딪히지 않게 합니다.
        /// 그러지 않으면 물건이 몸에 끼어 덜덜 떨립니다.
        /// </summary>
        private void IgnorePlayerCollision(Carryable target, bool ignore)
        {
            if (playerCollider == null || target == null) return;

            if (ignore)
            {
                ignoredColliders.Clear();
                target.GetComponentsInChildren<Collider>(true, ignoredColliders);
            }

            // <b>꺼져 있는 콜라이더에는 부르지 않습니다.</b> 유니티가 예외를 던지기 때문입니다.
            // 예외가 나면 Drop 이 도중에 끊겨 들린 표식과 꺼진 중력이 그대로 남고,
            // 그 물건은 다시는 집히지 않는 채 허공에 굳습니다.
            //
            // 되살릴 때(ignore == false) 꺼진 것을 건너뛰어도 됩니다.
            // 유니티는 콜라이더가 꺼졌다 켜지면 무시 상태를 스스로 지웁니다.
            if (!playerCollider.enabled || !playerCollider.gameObject.activeInHierarchy)
            {
                if (!ignore) ignoredColliders.Clear();
                return;
            }

            for (int i = 0; i < ignoredColliders.Count; i++)
            {
                Collider other = ignoredColliders[i];
                if (other == null || !other.enabled || !other.gameObject.activeInHierarchy) continue;

                Physics.IgnoreCollision(other, playerCollider, ignore);
            }

            if (!ignore) ignoredColliders.Clear();
        }
    }
}
