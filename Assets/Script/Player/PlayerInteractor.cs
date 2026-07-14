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

    [Tooltip("상호작용할 자동차 컨트롤러 (시동 걸기용)")]
    public CarController carController; // (CarController 스크립트가 필요합니다)

    [Tooltip("상호작용 키")]
    public KeyCode interactionKey = KeyCode.E;

    [Tooltip("상호작용 레이캐스트가 감지할 레이어")]
    public LayerMask interactionLayer;

    [Tooltip("음료 마시기 애니메이션을 재생할 UI 컨트롤러")]
    public DrinkAnimation drinkAnimator; // (DrinkAnimation 스크립트가 필요합니다)

    [Header("상호작용 효과")]
    [Tooltip("음료 마실 때 회복할 체력량")]
    public float healAmount = 10f;


    // --- Private Member Variables ---
    private BeverageBox currentBeverageBox; // (BeverageBox 스크립트가 필요합니다)
    private Transform cameraTransform;

    /// <summary>
    /// 스크립트가 처음 활성화될 때 카메라 Transform을 캐시합니다.
    /// </summary>
    void Start()
    {
        cameraTransform = transform; // 이 스크립트가 카메라에 붙어있다고 가정

        // --- 참조 확인 ---
        if (carController == null)
        {
            Debug.LogWarning("PlayerInteractor: CarController가 할당되지 않았습니다. 시동 걸기 및 체력 회복이 작동하지 않을 수 있습니다.");
        }
        if (drinkAnimator == null)
        {
            Debug.LogWarning("PlayerInteractor: DrinkAnimator가 할당되지 않았습니다. 음료 마시기 애니메이션이 작동하지 않습니다.");
        }
    }

    /// <summary>
    /// 매 프레임마다 호출됩니다.
    /// </summary>
    void Update()
    {
        HandleInteractionRaycast();
        HandleInteractionInput();
    }

    /// <summary>
    /// 카메라 정면으로 레이캐스트를 발사하여 상호작용 가능한 대상을 찾습니다.
    /// (원본 PlayerCameraController의 메서드)
    /// </summary>
    private void HandleInteractionRaycast()
    {
        RaycastHit hit;
        if (Physics.Raycast(cameraTransform.position, cameraTransform.forward, out hit, interactionDistance, interactionLayer))
        {
            BeverageBox box = hit.collider.GetComponent<BeverageBox>();
            currentBeverageBox = (box != null) ? box : null;
        }
        else
        {
            currentBeverageBox = null;
        }
    }

    /// <summary>
    /// 상호작용 키('E') 입력을 처리합니다.
    /// (원본 PlayerCameraController의 메서드)
    /// </summary>
    private void HandleInteractionInput()
    {
        if (Input.GetKeyDown(interactionKey))
        {
            // 1. 음료 상자 상호작용
            if (currentBeverageBox != null)
            {
                currentBeverageBox.TakeBeverage();

                if (drinkAnimator != null)
                {
                    drinkAnimator.PlayDrinkAnimation();
                }

                if (carController != null && carController.healthBar != null)
                {
                    carController.healthBar.Heal(healAmount);
                }
            }
            // 2. 자동차 시동 상호작용
            else if (carController != null)
            {
                Debug.Log("PlayerInteractor: 자동차 시동 토글 요청됨.");

                carController.ToggleEngine();
            }
        }
    }
}
