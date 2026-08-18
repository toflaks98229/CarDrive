using UnityEngine;

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

    [Tooltip("상호작용 키")]
    public KeyCode interactionKey = KeyCode.E;

    [Tooltip("상호작용 레이캐스트가 감지할 레이어")]
    public LayerMask interactionLayer;

    [Tooltip("음료 마시기 애니메이션을 재생할 UI 컨트롤러")]
    public DrinkAnimation drinkAnimator; // (DrinkAnimation 스크립트가 필요합니다)

    [Tooltip("상호작용 사운드를 재생할 컨트롤러. 비워두면 같은 오브젝트에서 찾습니다.")]
    public PlayerSoundController soundController;

    [Header("상호작용 효과")]
    [Tooltip("음료 마실 때 회복할 체력량")]
    public float healAmount = 10f;

    [Tooltip("음료로 회복할 플레이어 체력. 비워두면 씬에서 PlayerHealth를 찾습니다. " +
             "차량 내구도는 VehicleHealth라 타입이 달라 여기에 들어올 수 없습니다. " +
             "(예전에는 이 자리가 비면 차량 체력바로 대신해서, 음료를 마시면 차가 수리되었습니다)")]
    public PlayerHealth playerHealth;

    [Header("니즈 연동")]
    [Tooltip("니즈를 관리하는 시스템. 비워두면 씬에서 자동으로 찾습니다.")]
    public NeedsSystem needsSystem;

    [Tooltip("음료를 마셨을 때 해소되는 갈증")]
    public float beverageThirstRelief = 0.5f;

    [Tooltip("음료를 마셨을 때 차오르는 배뇨")]
    public float beverageUrineGain = 0.25f;

    [Header("문구 (다국어 대응)")]
    [Tooltip("{0}에는 키 이름이 들어갑니다.")]
    public string beverageFormat = "{0}: 음료 마시기";

    [Tooltip("{0}에는 키 이름, {1}에는 대상의 promptLabel이 들어갑니다.")]
    public string satisfierFormat = "{0}: {1}";


    // --- Private Member Variables ---
    private BeverageBox currentBeverageBox;   // 음료 상자는 회복·애니메이션이 얽혀 있어 따로 다룹니다.
    private IInteractable currentInteractable; // 그 밖의 모든 상호작용 대상 (문·운전대·침대 등)

    /// <summary>조준 광선을 쏠 기준 Transform입니다. 보통 메인 카메라입니다.</summary>
    private Transform cameraTransform;

    // --- Public Properties ---

    /// <summary>지금 조준점에 걸린 음료 상자입니다. (없으면 null)</summary>
    public BeverageBox CurrentBeverageBox { get { return currentBeverageBox; } }

    /// <summary>지금 조준점에 걸린 상호작용 대상입니다. (없으면 null)</summary>
    public IInteractable CurrentInteractable { get { return currentInteractable; } }

    /// <summary>조준점에 무언가 상호작용 가능한 것이 걸려 있는지 여부입니다.</summary>
    public bool HasTarget
    {
        get
        {
            if (currentBeverageBox != null) return true;
            return currentInteractable != null && currentInteractable.CanInteract();
        }
    }

    /// <summary>
    /// 지금 상호작용 키로 할 수 있는 일을 문장으로 돌려줍니다. 없으면 빈 문자열입니다.
    /// 조준점에 아무것도 걸리지 않았다면 항상 빈 문자열입니다.
    /// </summary>
    public string GetInteractionPrompt()
    {
        if (currentBeverageBox != null) return string.Format(beverageFormat, interactionKey);

        if (currentInteractable != null && currentInteractable.CanInteract())
        {
            string label = currentInteractable.GetInteractionLabel();
            if (!string.IsNullOrEmpty(label)) return string.Format(satisfierFormat, interactionKey, label);
        }

        return "";
    }

    /// <summary>
    /// 스크립트가 처음 활성화될 때 카메라 Transform을 캐시합니다.
    /// </summary>
    void Start()
    {
        cameraTransform = transform; // 이 스크립트가 카메라에 붙어있다고 가정

        // 사운드는 있으면 쓰고 없으면 조용히 넘어갑니다.
        if (soundController == null) soundController = GetComponent<PlayerSoundController>();

        // --- 참조 확인 ---
        // 이 컴포넌트는 차량 프리팹 안에 있고 플레이어 체력은 씬에 있어
        // 인스펙터로 직접 연결할 수 없습니다. 그래서 실행 중에 찾습니다.
        // PlayerHealth는 플레이어 전용 타입이라 차량 내구도가 잡힐 일이 없습니다.
        if (playerHealth == null)
        {
            playerHealth = FindAnyObjectByType<PlayerHealth>();
        }
        if (playerHealth == null)
        {
            Debug.LogWarning("PlayerInteractor: PlayerHealth를 찾지 못해 음료로 회복할 수 없습니다.", this);
        }

        if (drinkAnimator == null)
        {
            Debug.LogWarning("PlayerInteractor: DrinkAnimator가 할당되지 않았습니다. 음료 마시기 애니메이션이 작동하지 않습니다.");
        }

        if (needsSystem == null)
        {
            needsSystem = FindAnyObjectByType<NeedsSystem>();
        }
        if (needsSystem == null)
        {
            Debug.LogWarning("PlayerInteractor: NeedsSystem을 찾을 수 없습니다. 니즈 해소가 작동하지 않습니다.");
        }
    }

    /// <summary>
    /// 매 프레임마다 호출됩니다.
    /// </summary>
    void Update()
    {
        // 조준 표시는 계속 갱신하되, 오버레이가 떠 있으면 상호작용 키만 막습니다.
        HandleInteractionRaycast();

        if (GameInputGate.Suspended) return;

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
            currentBeverageBox = null;
            currentInteractable = null;
            return;
        }

        // 콜라이더가 자식에 있을 수 있으므로 부모까지 올라가며 찾습니다.
        currentBeverageBox = hit.collider.GetComponentInParent<BeverageBox>();
        currentInteractable = hit.collider.GetComponentInParent<IInteractable>();
    }

    /// <summary>
    /// 상호작용 키('E') 입력을 처리합니다.
    /// (원본 PlayerCameraController의 메서드)
    /// </summary>
    private void HandleInteractionInput()
    {
        // 배뇨 해소(P)는 UrineRelief가 따로 처리합니다.
        if (Input.GetKeyDown(interactionKey))
        {
            // 1. 음료 상자 상호작용
            if (currentBeverageBox != null)
            {
                // 상자가 비어 있으면 아무 효과도 없어야 합니다.
                // (예전에는 꺼내지 못해도 회복과 갈증 해소가 그대로 적용되었습니다)
                if (!currentBeverageBox.TakeBeverage())
                {
                    if (soundController != null) soundController.PlayInteractionFailSound();
                    return;
                }

                if (drinkAnimator != null)
                {
                    drinkAnimator.PlayDrinkAnimation();
                }

                if (soundController != null) soundController.PlayDrinkSound();

                // 음료는 플레이어를 회복시킵니다. 차량 내구도는 손대지 않습니다.
                if (playerHealth != null) playerHealth.Heal(healAmount);

                // 마시면 갈증이 풀리는 대신 배뇨가 차오릅니다.
                if (needsSystem != null)
                {
                    needsSystem.Satisfy(NeedType.Thirst, beverageThirstRelief);
                    needsSystem.Add(NeedType.Urine, beverageUrineGain);
                }
            }
            // 2. 그 밖의 상호작용 대상 (문 = 탑승, 운전대 = 시동, 침대·화장실·샤워 등)
            else if (currentInteractable != null && currentInteractable.CanInteract())
            {
                currentInteractable.Interact();
            }

            // 조준점에 아무것도 걸리지 않았다면 아무 일도 일어나지 않습니다.
            // (예전에는 여기서 시동이 걸렸지만, 이제 운전대를 봐야만 걸립니다)
        }
    }
}
