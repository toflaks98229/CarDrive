using UnityEngine;
using CarDrive.Common;
using CarDrive.Systems;

namespace CarDrive.Gameplay
{
    /// <summary>
    /// [신규]
    /// 플레이어의 상호작용 로직(레이캐스트, E키 입력)을 전담하는 클래스입니다.
    /// 이 컴포넌트는 PlayerCameraController와 같은 카메라 GameObject에 추가해야 합니다.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [Header("상호작용 설정")]
        [Tooltip("상호작용이 가능한 최대 거리")]
        public float interactionDistance = 3f;

        // 시동은 운전대(SteeringWheelInteractable)가 처리하므로 차량 참조가 필요 없습니다.

        // 상호작용 키는 GameInput이 소유합니다. (GameAction.Interact)
        // 안내 문구의 키 이름도 거기서 가져오므로, 키를 바꾸면 문구가 저절로 따라갑니다.

        [Tooltip("상호작용 레이캐스트가 감지할 레이어")]
        public LayerMask interactionLayer;

        [Tooltip("조준 광선을 쏠 기준. 비워두면 이 오브젝트가 카메라인지 확인하고, 아니면 Camera.main을 씁니다.")]
        public Transform aimSource;

        [Tooltip("상호작용 사운드를 재생할 컨트롤러. 비워두면 같은 오브젝트에서 찾습니다.")]
        public PlayerSoundController soundController;

        [Header("문구 (다국어 대응)")]
        [Tooltip("{0}에는 키 이름, {1}에는 대상의 promptLabel이 들어갑니다.")]
        public string satisfierFormat = "{0}: {1}";

        [Header("연동")]
        [Tooltip("마시는 중에는 상호작용을 막습니다. 비워두면 같은 오브젝트와 씬에서 찾습니다.")]
        public BeverageConsumer beverageConsumer;


        // --- Private Member Variables ---

        /// <summary>지금 조준점에 걸린 상호작용 대상입니다. (문·운전대·음료·침대 등)</summary>
        private IInteractable currentInteractable;

        /// <summary>조준 광선을 쏠 기준 Transform입니다. 보통 메인 카메라입니다.</summary>
        private Transform cameraTransform;

        // --- Public Properties ---

        /// <summary>지금 조준점에 걸린 상호작용 대상입니다. (없으면 null)</summary>
        public IInteractable CurrentInteractable { get { return currentInteractable; } }

        /// <summary>조준점에 무언가 상호작용 가능한 것이 걸려 있는지 여부입니다.</summary>
        public bool HasTarget
        {
            get { return currentInteractable != null && currentInteractable.CanInteract(); }
        }

        /// <summary>마시는 중이라 상호작용을 받지 않는 상태인지 여부입니다.</summary>
        public bool IsBlocked { get { return beverageConsumer != null && beverageConsumer.IsBusy; } }

        /// <summary>
        /// 지금 상호작용 키로 할 수 있는 일을 문장으로 돌려줍니다. 없으면 빈 문자열입니다.
        /// 조준점에 아무것도 걸리지 않았다면 항상 빈 문자열입니다.
        /// </summary>
        public string GetInteractionPrompt()
        {
            // 마시는 중에는 아무 안내도 띄우지 않습니다. 눌러도 받지 않기 때문입니다.
            if (IsBlocked) return "";

            if (currentInteractable != null && currentInteractable.CanInteract())
            {
                string label = currentInteractable.GetInteractionLabel();
                if (!string.IsNullOrEmpty(label))
                {
                    // 키 이름을 GameInput에서 가져옵니다. 재설정하면 안내 문구도 함께 바뀝니다.
                    return string.Format(satisfierFormat, GameInput.GetBindingName(GameAction.Interact), label);
                }
            }

            return "";
        }

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
        /// 스크립트가 처음 활성화될 때 카메라 Transform을 캐시합니다.
        /// </summary>
        void Start()
        {
            // "카메라에 붙어 있다"는 가정을 주석이 아니라 코드로 확인합니다.
            // 프리팹을 정리하다 다른 오브젝트로 옮겨가면 예전에는 조용히 엉뚱한 곳을 조준했습니다.
            cameraTransform = PlayerAim.Resolve(aimSource, this);

            // 사운드는 있으면 쓰고 없으면 조용히 넘어갑니다.
            if (soundController == null) soundController = GetComponent<PlayerSoundController>();

            // 마시는 중에는 상호작용을 막아야 하므로 그 상태를 알려 줄 컴포넌트를 찾아 둡니다.
            if (beverageConsumer == null) beverageConsumer = GetComponent<BeverageConsumer>();
            if (beverageConsumer == null) beverageConsumer = GameContext.Resolve<BeverageConsumer>(this);
        }

        /// <summary>
        /// 매 프레임마다 호출됩니다.
        /// </summary>
        void Update()
        {
            // 조준 표시는 계속 갱신합니다. 오버레이가 떠 있으면 GameInput이 키 입력만 막아 줍니다.
            HandleInteractionRaycast();
            HandleInteractionInput();
        }

        /// <summary>
        /// 화면 중앙(카메라 정면)으로 레이캐스트를 쏘아 조준점에 걸린 대상을 찾습니다.
        /// 원근 카메라에서 카메라의 정면 방향은 곧 화면 중앙이므로, 이 한 줄이 조준점 판정입니다.
        ///
        /// 상호작용 콜라이더는 트리거로 두기 때문에 QueryTriggerInteraction.Collide를 명시합니다.
        /// </summary>
        private void HandleInteractionRaycast()
        {
            RaycastHit hit;
            bool didHit = Physics.Raycast(
                cameraTransform.position,
                cameraTransform.forward,
                out hit,
                interactionDistance,
                interactionLayer,
                QueryTriggerInteraction.Collide
            );

            if (!didHit)
            {
                currentInteractable = null;
                return;
            }

            // 콜라이더가 자식에 있을 수 있으므로 부모까지 올라가며 찾습니다.
            // 음료와 음료 상자도 IInteractable이라 여기서 함께 잡힙니다.
            // (차 안에 있는 것은 각자 CanInteract에서 탑승 여부를 확인합니다)
            currentInteractable = hit.collider.GetComponentInParent<IInteractable>();
        }

        /// <summary>
        /// 상호작용 키('E') 입력을 처리합니다.
        /// (원본 PlayerCameraController의 메서드)
        /// </summary>
        private void HandleInteractionInput()
        {
            // 배뇨 해소(P)는 UrineRelief가 따로 처리합니다.
            if (!GameInput.InteractPressed) return;

            // 마시고 던지기가 끝날 때까지는 받지 않습니다.
            // 그러지 않으면 E를 연타해 상자를 순식간에 비울 수 있습니다.
            if (IsBlocked) return;

            // 문 = 탑승, 운전대 = 시동, 음료·상자 = 마시기, 침대·화장실 = 니즈 해소.
            // 대상이 무엇인지 여기서 구분하지 않습니다.
            if (currentInteractable != null && currentInteractable.CanInteract())
            {
                currentInteractable.Interact();
                return;
            }

            // 조준점에 아무것도 걸리지 않았다면 아무 일도 일어나지 않습니다.
        }
    }
}
